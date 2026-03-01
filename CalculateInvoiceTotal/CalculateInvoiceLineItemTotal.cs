using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using DataverseModel;
using wha.storey.core.plugins;


namespace wha.storey.core.plugins
{
    public sealed class CalculateInvoiceLineItemTotalPlugin : PluginBase
    {
        public CalculateInvoiceLineItemTotalPlugin(string unsecure, string secure)
            : base(typeof(CalculateInvoiceLineItemTotalPlugin))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localContext)
        {
            if (localContext == null)
                throw new InvalidPluginExecutionException(nameof(localContext));

            var ctx = localContext.PluginExecutionContext;
            var svc = localContext.InitiatingUserService;
            var trace = localContext.TracingService;

            if (ctx.Depth > 2)
                return;

            object obj;
            if (!ctx.InputParameters.TryGetValue("Target", out obj))
                return;

            var target = obj as Entity;
            if (target == null)
                return;

            if (!string.Equals(target.LogicalName, WHa_InvoiceLineItem.EntityLogicalName, StringComparison.OrdinalIgnoreCase))
                return;

            var sourceType = target.GetAttributeValue<string>("wha_sourceidtype");
            var sourceRef = target.GetAttributeValue<EntityReference>("wha_sourceid");

            if ((string.IsNullOrWhiteSpace(sourceType) || sourceRef == null) &&
                ctx.PreEntityImages != null &&
                ctx.PreEntityImages.Contains("PreImage"))
            {
                var pre = ctx.PreEntityImages["PreImage"];
                if (pre != null)
                {
                    if (string.IsNullOrWhiteSpace(sourceType))
                        sourceType = pre.GetAttributeValue<string>("wha_sourceidtype");

                    if (sourceRef == null)
                        sourceRef = pre.GetAttributeValue<EntityReference>("wha_sourceid");
                }
            }

            if (string.IsNullOrWhiteSpace(sourceType))
                return;

            if (sourceRef == null || sourceRef.Id == Guid.Empty)
                return;

            var sourceEntity = MapSourceTypeToEntityLogicalName(sourceType);
            if (sourceEntity == null)
            {  
                return; 
            }

            if (string.Equals(sourceEntity, "account", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var amount = RetrieveAmount(svc, sourceEntity, sourceRef.Id);
            if (amount == null)
            {
                return;
            }

            target["wha_unitprice"] = amount;
            target["wha_totallineitemamount"] = amount;
        }

        private static string MapSourceTypeToEntityLogicalName(string sourceType)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
                return null;

            var st = sourceType.Trim().ToLowerInvariant();

            switch (st)
            {
                case "wha_rent":
                case "wha_fee":
                case "wha_discount":
                case "account":
                    return st;
                default:
                    return null;
            }
        }

        private static Money RetrieveAmount(IOrganizationService svc, string logicalName, Guid id)
        {
            try
            {
                var src = svc.Retrieve(logicalName, id, new ColumnSet("wha_amount"));
                return src.GetAttributeValue<Money>("wha_amount");
            }
            catch
            {
                return null;
            }
        }
    }
}
