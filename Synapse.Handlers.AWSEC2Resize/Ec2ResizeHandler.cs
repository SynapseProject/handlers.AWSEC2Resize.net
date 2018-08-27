using Synapse.Core;
using Synapse.Handlers.AWSEC2Resize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon.EC2;
using Amazon.EC2.Model;
using Newtonsoft.Json;
using StatusType = Synapse.Core.StatusType;

public class Ec2ResizeHandler : HandlerRuntimeBase
{
    private HandlerConfig _config;
    private int _sequenceNumber = 0;
    private string _mainProgressMsg = "";
    private string _subProgressMsg = "";
    private string _context = "Execute";
    private bool _encounteredFailure = false;
    private string _returnFormat = "json"; // Default return format
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = int.MaxValue
    };

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        ResizeResponse response = new ResizeResponse // Handler response
        {
            Results = new List<ResizeResult>()
        };

        try
        {
            _mainProgressMsg = "Deserializing incoming request...";
            UpdateProgress(_mainProgressMsg, StatusType.Initializing);
            ResizeRequest parms = DeserializeOrNew<ResizeRequest>(startInfo.Parameters);

            _mainProgressMsg = "Processing individual child request...";
            UpdateProgress(_mainProgressMsg, StatusType.Running);

            if (parms?.Details != null)
            {
                foreach (ResizeDetail detail in parms.Details)
                {
                    bool subTaskSucceed = false;
                    try
                    {
                        _subProgressMsg = "Verifying child request parameters...";
                        UpdateProgress(_subProgressMsg);
                        if (ValidateRequest(detail))
                        {
                            _subProgressMsg = "Executing request" + (startInfo.IsDryRun ? " in dry run mode..." : "...");
                            UpdateProgress(_subProgressMsg);
                            subTaskSucceed = ExecuteEc2Resize(detail, startInfo.IsDryRun); // TODO: Complete this
                            _subProgressMsg = "Processed child request.";
                            UpdateProgress(_subProgressMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        _subProgressMsg = ex.Message;
                        UpdateProgress(_subProgressMsg);
                        subTaskSucceed = false;
                    }
                    finally
                    {
                        response.Results.Add(new ResizeResult()
                        {
                            ExitCode = subTaskSucceed ? 0 : -1,
                            ExitSummary = _subProgressMsg,
                            Environment = detail.Environment,
                            InstanceId = detail.InstanceId,
                            NewInstanceType = detail.NewInstanceType,
                            Region = detail.Region
                        });
                    }
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

    //    private void ProcessRequest(Ec2ResizeRequest parms)
    //    {
    //        string profile;
    //        _config.AwsEnvironmentProfile.TryGetValue(parms.Environment, out profile);
    //
    //        // Is instance stopped
    //        Instance instance = AwsServices.GetInstance(parms.InstanceId, parms.Region, profile);
    //
    //        if (instance != null)
    //        {
    //            if (instance.InstanceType == InstanceType.FindValue(parms.InstanceType.ToLower()))
    //            {
    //                _response.ExitCode = 0;
    //                _response.Summary = "EC2 instance is already of the given type.";
    //            }
    //            else if (instance.State.Name != InstanceStateName.Stopped)
    //            {
    //                if (parms.StopRunningInstance)
    //                {
    //
    //                }
    //            }
    //        }
    //        else
    //        {
    //            throw new Exception("Specified instance is not found.");
    //        }
    //        // Stop instance
    //
    //
    //        // Change instance type
    //    }

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
        _mainProgressMsg = _mainProgressMsg + Environment.NewLine + message;
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
        OnProgress(_context, _mainProgressMsg, _result.Status, _sequenceNumber);
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
        //        string profile;
        //        _config.AwsEnvironmentProfile.TryGetValue(parms.Environment, out profile);
        //
        //        // Is instance stopped
        //        Instance instance = AwsServices.GetInstance(parms.InstanceId, parms.Region, profile);
        //
        //        if (instance != null)
        //        {
        //            if (instance.InstanceType == InstanceType.FindValue(parms.InstanceType.ToLower()))
        //            {
        //                _response.ExitCode = 0;
        //                _response.Summary = "EC2 instance is already of the given type.";
        //            }
        //            else if (instance.State.Name != InstanceStateName.Stopped)
        //            {
        //                if (parms.StopRunningInstance)
        //                {
        //
        //                }
        //            }
        //        }
        //        else
        //        {
        //            throw new Exception("Specified instance is not found.");
        //        }
        //        // Stop instance
        //
        //
        //        // Change instance type

        bool isSuccess = false;


        string profile;
        _config.AwsEnvironmentProfile.TryGetValue(request.Environment, out profile);

        if (!string.IsNullOrWhiteSpace(profile))
        {
            // Describe instance
            Instance instance = AwsServices.GetInstance(request.InstanceId, request.Region, profile);

            // Check if instance type is different
            if (instance != null)
            {
                if (instance.InstanceType != request.NewInstanceType.ToLower())
                {
                    AwsServices.StopInstance(request.InstanceId, request.Region, profile);

                    string state;
                    do
                    {
                        Thread.Sleep(5000);
                        instance = AwsServices.GetInstance(request.InstanceId, request.Region, profile);
                        state = instance.State.Name.Value;
                    } while (state != "stopped");

                    AwsServices.ModifyInstance(request.InstanceId, request.NewInstanceType, request.Region, profile);

                    if (request.StartStoppedInstance)
                    {
                        AwsServices.StartInstance(request.InstanceId, request.Region, profile);
                    }
                }
                else
                {
                    throw new Exception($"The instance is already of type '{request.NewInstanceType}'.");
                }
            }
            else
            {
                throw new Exception("Failed to obtain the EC2 instance detail.");
            }
            // If it is the same, no action is taken

            // If different and allow stopping running instance, stop the instance

            // Wait until instance is stopped

            // Resize the instance

            // If instructed to start the instance, start the instance
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

