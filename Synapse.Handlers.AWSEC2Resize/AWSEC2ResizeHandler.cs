using Synapse.Core;
using Synapse.Handlers.AWSEC2Resize;
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.EC2;
using Amazon.EC2.Model;
using StatusType = Synapse.Core.StatusType;

public class AWSEC2ResizeHandler : HandlerRuntimeBase
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
    private readonly Ec2Response _response = new Ec2Response();

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string message;

        try
        {
            message = "Deserializing incoming request...";
            UpdateProgress(message, StatusType.Initializing);
            Ec2Request parms = DeserializeOrNew<Ec2Request>(startInfo.Parameters);

            message = "Processing request...";
            UpdateProgress(message, StatusType.Running);
            ValidateRequest(parms);
            ProcessRequest(parms);
        }
        catch (Exception ex)
        {
            UpdateProgress(ex.Message, StatusType.Failed);
            _encounteredFailure = true;
            _response.Summary = ex.Message;
            _response.ExitCode = -1;
        }
        finally
        {
            message = "Serializing response...";
            UpdateProgress(message);
            try
            {
                _result.ExitData = _response;
            }
            catch (Exception ex)
            {
                _result.ExitData = ex.Message;
            }
        }
        return _result;
    }

    private void ProcessRequest(Ec2Request parms)
    {
        string profile;
        _config.AwsEnvironmentProfile.TryGetValue(parms.Environment, out profile);

        // Is instance stopped
        Instance instance = AwsServices.GetInstance(parms.InstanceId, parms.Region, profile);

        if (instance != null)
        {
            if (instance.InstanceType == InstanceType.FindValue(parms.InstanceType.ToLower()))
            {
                _response.ExitCode = 0;
                _response.Summary = "EC2 instance is already of the given type.";
            }
            else if (instance.State.Name != InstanceStateName.Stopped)
            {
                if (parms.StopRunningInstance)
                {
                    
                }
            }
        }
        else
        {
            throw new Exception("Specified instance is not found.");
        }
        // Stop instance


        // Change instance type


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
        return new Ec2Request()
        {
            Environment = "ENV1",
            Region = "us-west-1",
            InstanceId = "i-xxxxxx",
            InstanceType = "t2.nano",
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

    private void ValidateRequest(Ec2Request parms)
    {
        if (!IsNullRequest(parms))
        {
            if (!IsValidEnvironment(parms.Environment))
            {
                throw new Exception("Environment can not be found.");
            }
            if (!AwsServices.IsValidRegion(parms.Region))
            {
                throw new Exception("AWS region is not valid.");
            }
            if (!AwsServices.IsValidInstanceType(parms.InstanceType))
            {
                throw new Exception("EC2 instance type is not valid.");
            }
            if (!SetReturnFormat(parms.ReturnFormat))
            {
                throw new Exception("Valid return formats are json, xml or yaml.");
            }
        }
        else
        {
            throw new Exception("No parameter is found in the request.");
        }
    }

    private bool IsNullRequest(Ec2Request parms)
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

