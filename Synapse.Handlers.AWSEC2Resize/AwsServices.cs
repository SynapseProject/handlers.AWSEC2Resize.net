using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Synapse.Handlers.AWSEC2Resize
{
    public class AwsServices
    {
        public static AWSCredentials GetAWSCredentials(string profileName = "", string credentialFile = null)
        {
            if (String.IsNullOrWhiteSpace(profileName))
            {
                profileName = "default";
            }
            AWSCredentials awsCredentials = null;

            CredentialProfileStoreChain chain = new CredentialProfileStoreChain(credentialFile);

            chain.TryGetAWSCredentials(profileName, out awsCredentials);

            return awsCredentials;
        }

        public static CredentialProfile GetCredentialProfile(string profileName = "")
        {
            if (String.IsNullOrWhiteSpace(profileName))
            {
                profileName = "default";
            }
            CredentialProfile credentialProfile = null;

            CredentialProfileStoreChain chain = new CredentialProfileStoreChain();

            chain.TryGetProfile(profileName, out credentialProfile);

            return credentialProfile;
        }

        public static bool IsValidRegion(string region)
        {
            return !String.IsNullOrWhiteSpace(region) && !RegionEndpoint.GetBySystemName(region).DisplayName.Contains("Unknown");
        }

        public static bool IsValidInstanceType(string instanceType)
        {
            string[] validInstanceTypes = {
                "t2.nano", "t2.micro", "t2.small", "t2.medium", "t2.large", "t2.xlarge", "t2.2xlarge",
                "m5.large", "m5.xlarge", "m5.2xlarge", "m5.4xlarge", "m5.12xlarge", "m5.24xlarge",
                "m5d.large", "m5d.xlarge", "m5d.2xlarge", "m5d.4xlarge", "m5d.12xlarge", "m5d.24xlarge",
                "m4.large", "m4.xlarge", "m4.2xlarge", "m4.4xlarge", "m4.10xlarge", "m4.16xlarge"};

            return Array.IndexOf(validInstanceTypes, instanceType) > -1 ? true : false;
        }

        public static Instance GetInstance(string instanceId, string regionName, string profileName, string credentialFile = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new Exception("Instance id is not specified.");
            }

            AWSCredentials creds = GetAWSCredentials(profileName, credentialFile);

            if (creds == null)
            {
                throw new Exception("AWS credentials are not specified.");
            }

            RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(regionName);
            if (endpoint.DisplayName.Contains("Unknown"))
            {
                throw new Exception("AWS region endpoint is not valid.");
            }

            List<Instance> instances = new List<Instance>();
            Instance foundInstance;

            try
            {
                using (AmazonEC2Client client = new AmazonEC2Client(creds, endpoint))
                {
                    DescribeInstancesRequest req = new DescribeInstancesRequest
                    {
                        InstanceIds = { instanceId }
                    };
                    do
                    {
                        DescribeInstancesResponse resp = client.DescribeInstances(req);
                        if (resp != null)
                        {
                            instances.AddRange(resp.Reservations.SelectMany(reservation => reservation.Instances).Where(x => x.InstanceId == instanceId));
                            req.NextToken = resp.NextToken;
                        }
                    } while (!string.IsNullOrWhiteSpace(req.NextToken));
                }

                if (instances.Count == 1)
                {
                    foundInstance = instances[0];
                }
                else
                {
                    throw new Exception("Error finding the specified instance.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Encountered exception while describing EC2 instances: {ex.Message}");
            }

            return foundInstance;
        }

        public static void StopInstance(string instanceId, string regionName, string profileName, string credentialFile = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new Exception("Instance id is not specified.");
            }

            AWSCredentials creds = GetAWSCredentials(profileName, credentialFile);

            if (creds == null)
            {
                throw new Exception("AWS credentials are not specified");
            }

            RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(regionName);
            if (endpoint.DisplayName.Contains("Unknown"))
            {
                throw new Exception("AWS region endpoint is not valid.");
            }

            try
            {
                using (AmazonEC2Client client = new AmazonEC2Client(creds, endpoint))
                {
                    StopInstancesRequest req = new StopInstancesRequest
                    {
                        InstanceIds = new List<string>() { instanceId }
                    };
                    client.StopInstances(req);
                }
            }
            catch (AmazonEC2Exception ex)
            {
                // Check the ErrorCode to see if the instance does not exist.
                if ("InvalidInstanceID.NotFound" == ex.ErrorCode)
                {
                    throw new Exception($"EC2 instance {instanceId} does not exist.");
                }
                // The exception was thrown for another reason, so re-throw the exception.
                throw;
            }
        }

        public static void ModifyInstance(string instanceId, string instanceType, string regionName, string profileName, string credentialFile = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new Exception("Instance id is not specified.");
            }

            AWSCredentials creds = GetAWSCredentials(profileName, credentialFile);

            if (creds == null)
            {
                throw new Exception("AWS credentials are not specified");
            }

            RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(regionName);
            if (endpoint.DisplayName.Contains("Unknown"))
            {
                throw new Exception("AWS region endpoint is not valid.");
            }

            try
            {
                using (AmazonEC2Client client = new AmazonEC2Client(creds, endpoint))
                {
                    ModifyInstanceAttributeRequest req = new ModifyInstanceAttributeRequest
                    {
                        InstanceId = instanceId,
                        InstanceType = instanceType
                    };
                    client.ModifyInstanceAttribute(req);
                }
            }
            catch (AmazonEC2Exception ex)
            {
                // Check the ErrorCode to see if the instance does not exist.
                if ("InvalidInstanceID.NotFound" == ex.ErrorCode)
                {
                    throw new Exception($"EC2 instance {instanceId} does not exist.");
                }

                // The exception was thrown for another reason, so re-throw the exception.
                throw;
            }
        }

        public static void StartInstance(string instanceId, string regionName, string profileName, string credentialFile = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new Exception("Instance id is not specified.");
            }

            AWSCredentials creds = GetAWSCredentials(profileName, credentialFile);

            if (creds == null)
            {
                throw new Exception("AWS credentials are not specified");
            }

            RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(regionName);
            if (endpoint.DisplayName.Contains("Unknown"))
            {
                throw new Exception("AWS region endpoint is not valid.");
            }

            try
            {
                using (AmazonEC2Client client = new AmazonEC2Client(creds, endpoint))
                {
                    StartInstancesRequest req = new StartInstancesRequest
                    {
                        InstanceIds = new List<string>() { instanceId }
                    };
                    client.StartInstances(req);
                }
            }
            catch (AmazonEC2Exception ex)
            {
                // Check the ErrorCode to see if the instance does not exist.
                if ("InvalidInstanceID.NotFound" == ex.ErrorCode)
                {
                    throw new Exception($"EC2 instance {instanceId} does not exist.");
                }

                // The exception was thrown for another reason, so re-throw the exception.
                throw;
            }
        }
    }
}
