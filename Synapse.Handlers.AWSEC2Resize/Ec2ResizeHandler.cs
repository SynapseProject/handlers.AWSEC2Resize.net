using Amazon.EC2.Model;
using Newtonsoft.Json;
using Synapse.Core;
using Synapse.Handlers.AWSEC2Resize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StatusType = Synapse.Core.StatusType;

public class Ec2ResizeHandler : HandlerRuntimeBase
{
    private HandlerConfig _config;
    private int _sequenceNumber = 0;
    private string _mainProgressMsg = "";
    private string _context = "Execute";
    private bool _encounteredFailure = false;
    private string _returnFormat = "json"; // Default return format
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = int.MaxValue
    };
    private const int FiveMinutes = 5000 * 60;

    public override IHandlerRuntime Initialize(string values)
    {
        _config = DeserializeOrNew<HandlerConfig>(values);

        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        ResizeResponse response = new ResizeResponse(); // Handler response

        try
        {
            _mainProgressMsg = "Deserializing incoming request...";
            UpdateProgress(_mainProgressMsg, StatusType.Initializing);
            ResizeDetail parms = DeserializeOrNew<ResizeDetail>(startInfo.Parameters);

            _mainProgressMsg = "Processing request...";
            UpdateProgress(_mainProgressMsg, StatusType.Running);

            if (parms != null)
            {
                bool subTaskSucceed = false;
                try
                {
                    _mainProgressMsg = "Verifying request parameters...";
                    UpdateProgress(_mainProgressMsg);
                    if (ValidateRequest(parms))
                    {
                        _mainProgressMsg = "Executing request" + (startInfo.IsDryRun ? " in dry run mode..." : "...");
                        UpdateProgress(_mainProgressMsg);
                        subTaskSucceed = ExecuteEc2Resize(parms, startInfo.IsDryRun); // TODO: Complete this
                        _mainProgressMsg = "Processed request.";
                        UpdateProgress(_mainProgressMsg);
                    }
                }
                catch (Exception ex)
                {
                    _mainProgressMsg = ex.Message;
                    UpdateProgress(_mainProgressMsg);
                    subTaskSucceed = false;
                }
                finally
                {
                    Console.WriteLine(_mainProgressMsg);
                    response.Results = new ResizeResult()
                    {
                        ExitCode = subTaskSucceed ? 0 : -1,
                        ExitSummary = _mainProgressMsg,
                        Environment = parms.Environment,
                        InstanceId = parms.InstanceId,
                        NewInstanceType = parms.NewInstanceType,
                        Region = parms.Region
                    };
                }
                _result.Status = StatusType.Complete;
            }
            else
            {
                _result.Status = StatusType.Failed;
                _mainProgressMsg = "No server resize detail is found from the incoming request.";
                UpdateProgress(_mainProgressMsg, StatusType.Failed);
            }
        }
        catch (Exception ex)
        {
            _result.Status = StatusType.Failed;
            _mainProgressMsg = $"Execution has been aborted due to: {ex.Message}";
            UpdateProgress(_mainProgressMsg, StatusType.Failed);
        }

        _mainProgressMsg = startInfo.IsDryRun ? "Dry run execution is completed." : "Execution is completed.";
        response.Summary = _mainProgressMsg;
        _result.ExitData = JsonConvert.SerializeObject(response);
        return _result;
    }

    public override object GetConfigInstance()
    {
        return new HandlerConfig
        {
            AwsEnvironmentProfile = new Dictionary<string, string>
            {
                { "ENV1", "AWSPROFILE1" },
                { "ENV2", "AWSPROFILE2" }
            }
        };
    }

    public override object GetParametersInstance()
    {
        return new ResizeDetail()
        {
            Environment = "ENV1",
            Region = "us-west-1",
            InstanceId = "i-xxxxxx",
            NewInstanceType = "t2.nano",
            StopRunningInstance = true,
            StartStoppedInstance = true
        };
    }

    private void UpdateProgress(string message, StatusType status = StatusType.Any, int seqNum = -1)
    {
        if (status != StatusType.Any)
        {
            _result.Status = status;
        }
        if (seqNum == 0)
        {
            _sequenceNumber = int.MaxValue;
        }
        else
        {
            _sequenceNumber++;
        }
        OnProgress(_context, message, _result.Status, _sequenceNumber);
    }

    private bool ValidateRequest(ResizeDetail parms)
    {
        bool areValid = true;
        if (!IsNullRequest(parms))
        {
            if (!IsValidEnvironment(parms.Environment))
            {
                UpdateProgress("Environment can not be found.");
                areValid = false;
            }
            if (!AwsServices.IsValidRegion(parms.Region))
            {
                UpdateProgress("AWS region is not valid.");
                areValid = false;
            }
            if (!AwsServices.IsValidInstanceType(parms.NewInstanceType))
            {
                UpdateProgress("EC2 instance type is not valid.");
                areValid = false;
            }
            if (!SetReturnFormat(parms.ReturnFormat))
            {
                UpdateProgress("Valid return formats are json, xml or yaml.");
                areValid = false;
            }
        }
        else
        {
            UpdateProgress("No parameter is found in the request.");
            areValid = false;
        }

        return areValid;
    }

    private bool IsNullRequest(ResizeDetail parms)
    {
        bool isNull = true;

        if (parms != null)
        {
            isNull = parms.GetType().GetProperties().All(p => p.GetValue(parms) == null);
        }
        return isNull;
    }

    private bool IsValidEnvironment(string environment)
    {
        return !string.IsNullOrWhiteSpace(environment) && _config.AwsEnvironmentProfile.ContainsKey(environment);
    }

    public bool ExecuteEc2Resize(ResizeDetail request, bool isDryRun = false)
    {
        bool isSuccess = false;


        string profile;
        _config.AwsEnvironmentProfile.TryGetValue(request.Environment, out profile);

        if (!string.IsNullOrWhiteSpace(profile))
        {
            UpdateProgress("Getting EC2 instance details...");
            Instance instance = AwsServices.GetInstance(request.InstanceId, request.Region, profile, _config.CredentialFile);

            // Check if instance type is different
            if (instance != null)
            {
                if (instance.InstanceType != request.NewInstanceType.ToLower())
                {
                    // If different and allow stopping running instance, stop the instance
                    if (!isDryRun)
                    {
                        UpdateProgress("Stopping the EC2 instance...");
                        AwsServices.StopInstance(request.InstanceId, request.Region, profile, _config.CredentialFile);

                        string state;
                        int counter = 5000;

                        do
                        {
                            UpdateProgress("Waiting for EC2 to be stopped...");
                            if (counter > FiveMinutes)
                            {
                                throw new Exception("Failed to stop the EC2 instance within 5 minutes. Aborting the resizing operation.");
                            }
                            Thread.Sleep(5000);
                            instance = AwsServices.GetInstance(request.InstanceId, request.Region, profile, _config.CredentialFile);
                            state = instance.State.Name.Value;
                            counter += 5000;
                        } while (state != "stopped");


                        UpdateProgress("Changing the EC2's instance type...");
                        AwsServices.ModifyInstance(request.InstanceId, request.NewInstanceType, request.Region, profile, _config.CredentialFile);

                        if (request.StartStoppedInstance)
                        {
                            UpdateProgress("Starting the EC2 instance...");
                            AwsServices.StartInstance(request.InstanceId, request.Region, profile, _config.CredentialFile);
                        }
                    }
                }
                else
                {
                    throw new Exception($"The instance is already of type '{request.NewInstanceType}'.");
                }
            }
            else
            {
                // If it is the same, no action is taken
                throw new Exception("Failed to obtain the EC2 instance detail.");
            }
            isSuccess = true;
        }
        else
        {
            throw new Exception("Specified environment is not found.");
        }

        return isSuccess;
    }

    private bool SetReturnFormat(string format)
    {
        bool isValid = true;
        if (string.IsNullOrWhiteSpace(format))
        {
            _returnFormat = "json";
        }
        else if (string.Equals(format, "json", StringComparison.CurrentCultureIgnoreCase))
        {
            _returnFormat = "json";
        }
        else if (string.Equals(format, "xml", StringComparison.CurrentCultureIgnoreCase))
        {
            _returnFormat = "xml";
        }
        else if (string.Equals(format, "yaml", StringComparison.CurrentCultureIgnoreCase))
        {
            _returnFormat = "yaml";
        }
        else
        {
            isValid = false;
        }
        return isValid;
    }
}

