// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using System;
using System.IO;
using System.Threading;

namespace ManageWebAppWithDomainSsl
{
    public class Program
    {
        private static readonly string CertificatePassword = Utilities.CreatePassword();

        /**
         * Azure App Service sample for managing web apps.
         *  - app service plan, web app
         *    - Create 2 web apps under the same new app service plan
         *  - domain
         *    - Create a domain
         *  - certificate
         *    - Upload a self-signed wildcard certificate
         *    - update both web apps to use the domain and the created wildcard SSL certificate
         */
        public static async Task RunSample(ArmClient client)
        {
            AzureLocation region = AzureLocation.EastUS;
            string websiteName = Utilities.CreateRandomName("website-");
            string planName = Utilities.CreateRandomName("plan-");
            string app1Name = Utilities.CreateRandomName("webapp1-");
            string app2Name = Utilities.CreateRandomName("webapp2-");
            string rgName = Utilities.CreateRandomName("rgNEMV_");
            string domainName = Utilities.CreateRandomName("jsdkdemo-") + ".com";
            var lro = await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;

            try
            {
                //============================================================
                // Create a web app with a new app service plan

                Utilities.Log("Creating web app " + app1Name + "...");

                var webSite1Collection = resourceGroup.GetWebSites();
                var webSite1Data = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                    }
                };
                var webSite1_lro = await webSite1Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, app1Name, webSite1Data);
                var webSite1 = webSite1_lro.Value;

                Utilities.Log("Created web app " + webSite1.Data.Name);
                Utilities.Print(webSite1);

                //============================================================
                // Create a second web app with the same app service plan

                Utilities.Log("Creating another web app " + app2Name + "...");
                var plan = webSite1.Data.AppServicePlanId;
                var webSite2Collection = resourceGroup.GetWebSites();
                var webSite2Data = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                    }
                };
                var webSite2_lro = await webSite2Collection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, websiteName, webSite2Data);
                var webSite2 = webSite2_lro.Value;

                Utilities.Log("Created web app " + webSite2.Data.Name);
                Utilities.Print(webSite2);

                //============================================================
                // Purchase a domain (will be canceled for a full refund)

                Utilities.Log("Purchasing a domain " + domainName + "...");

                var domainCollection = resourceGroup.GetAppServiceDomains();
                var domainData = new AppServiceDomainData(region)
                {
                    ContactRegistrant = new RegistrationContactInfo("jondoe@contoso.com", "Jon", "Doe", "4258828080")
                    {
                        AddressMailing = new RegistrationAddressInfo("123 4th Ave", "Redmond", "UnitedStates", "98052", "WA")
                    },
                    IsDomainPrivacyEnabled = true,
                    IsAutoRenew = false
                };
                var domain_lro = domainCollection.CreateOrUpdate(WaitUntil.Completed, domainName, domainData);
                var domain = domain_lro.Value;
                Utilities.Log("Purchased domain " + domain.Data.Name);
                Utilities.Print(domain);

                //============================================================
                // Bind domain to web app 1

                Utilities.Log("Binding http://" + app1Name + "." + domainName + " to web app " + app1Name + "...");

                var bindingsCollection = webSite1.GetSiteHostNameBindings();
                var bindingsdata = new HostNameBindingData()
                {
                    DomainId = domain.Id,
                    CustomHostNameDnsRecordType = CustomHostNameDnsRecordType.CName,
                };
                var bindings_lro = bindingsCollection.CreateOrUpdate(WaitUntil.Completed, Utilities.CreateRandomName("bindings-"), bindingsdata);
                var bindings = bindings_lro.Value;

                Utilities.Log("Finished binding http://" + app1Name + "." + domainName + " to web app " + app1Name);
                Utilities.Print(bindings);

                //============================================================
                // Create a self-singed SSL certificate

                var pfxPath = "webapp_" + nameof(ManageWebAppWithDomainSsl).ToLower() + ".pfx";

                Utilities.Log("Creating a self-signed certificate " + pfxPath + "...");

                Utilities.CreateCertificate(domainName, pfxPath, CertificatePassword);

                Utilities.Log("Created self-signed certificate " + pfxPath);

                //============================================================
                // Bind domain to web app 2 and turn on wild card SSL for both

                Utilities.Log("Binding https://" + app1Name + "." + domainName + " to web app " + app1Name + "...");

                var bindingsCollection1 = webSite1.GetSiteHostNameBindings();
                var bindingsdata1 = new HostNameBindingData()
                {
                    DomainId = domain.Id,
                    CustomHostNameDnsRecordType = CustomHostNameDnsRecordType.CName,
                    SslState = HostNameBindingSslState.SniEnabled
                };
                var bindings_lro1 = bindingsCollection.CreateOrUpdate(WaitUntil.Completed, Utilities.CreateRandomName("bindings-"), bindingsdata1);
                var bindings1 = bindings_lro.Value;

                Utilities.Log("Finished binding http://" + app1Name + "." + domainName + " to web app " + app1Name);
                Utilities.Print(bindings1);

                Utilities.Log("Binding https://" + app2Name + "." + domainName + " to web app " + app2Name + "...");

                var bindingsCollection2 = webSite2.GetSiteHostNameBindings();
                var bindingsdata2 = new HostNameBindingData()
                {
                    DomainId = domain.Id,
                    CustomHostNameDnsRecordType = CustomHostNameDnsRecordType.CName,
                    SslState = HostNameBindingSslState.SniEnabled,

                };
                var bindings_lro2 = bindingsCollection.CreateOrUpdate(WaitUntil.Completed, Utilities.CreateRandomName("bindings-"), bindingsdata2);
                var bindings2 = bindings_lro.Value;

                Utilities.Log("Finished binding http://" + app2Name + "." + domainName + " to web app " + app2Name);
                Utilities.Print(bindings2);
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    await resourceGroup.DeleteAsync(WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}