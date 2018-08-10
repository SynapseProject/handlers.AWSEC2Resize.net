using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Synapse.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        }
        catch (Exception ex) { }

        return null;
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
            if (!IsValidRegion(parms.Region))
            {
                throw new Exception("AWS region is not valid.");
            }
            if (!IsValidInstanceType(parms.InstanceType))
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

    private static bool IsValidRegion(string region)
    {
        return !string.IsNullOrWhiteSpace(region) && !RegionEndpoint.GetBySystemName(region).DisplayName.Contains("Unknown");
    }

    private static AWSCredentials GetAwsCredentials(string profileName = "")
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            profileName = "default";
        }
        AWSCredentials awsCredentials = null;

        CredentialProfileStoreChain chain = new CredentialProfileStoreChain();

        chain.TryGetAWSCredentials(profileName, out awsCredentials);

        return awsCredentials;
    }

    private static CredentialProfile GetCredentialProfile(string profileName = "")
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            profileName = "default";
        }
        CredentialProfile credentialProfile = null;

        CredentialProfileStoreChain chain = new CredentialProfileStoreChain();

        chain.TryGetProfile(profileName, out credentialProfile);

        return credentialProfile;
    }

    private static bool IsValidInstanceType(string instanceType)
    {
        string[] validInstanceTypes = {
            "t2.nano", "t2.micro", "t2.small", "t2.medium", "t2.large", "t2.xlarge", "t2.2xlarge",
            "m5.large", "m5.xlarge", "m5.2xlarge", "m5.4xlarge", "m5.12xlarge", "m5.24xlarge",
            "m5d.large", "m5d.xlarge", "m5d.2xlarge", "m5d.4xlarge", "m5d.12xlarge", "m5d.24xlarge",
            "m4.large", "m4.xlarge", "m4.2xlarge", "m4.4xlarge", "m4.10xlarge", "m4.16xlarge"};

        return Array.IndexOf(validInstanceTypes, instanceType) > -1 ? true : false;
    }
}
