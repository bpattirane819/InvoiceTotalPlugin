using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using DataverseModel;
using wha.storey.core.plugins;


namespace wha.storey.core.plugins
{
    public sealed class CalculateInvoiceTotalPlugin : PluginBase
    {
        public CalculateInvoiceTotalPlugin(string unsecure, string secure)
            : base(typeof(CalculateInvoiceTotalPlugin))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new InvalidPluginExecutionException(nameof(localContext));
            }

            var ctx = localContext.PluginExecutionContext;
            var svc = localContext.InitiatingUserService;
            var trace = localContext.TracingService;

            // Guard against recursive plugin execution.
            // This plugin updates the Invoice, which can trigger plugins again and again every time an update happens.
            // Depth > 2 means we're reacting to our own update, so exit to avoid loops.
            if (ctx.Depth > 2)
            {
                trace.Trace($"CalculateInvoiceTotalPlugin: Exiting due to Depth={ctx.Depth}");
                return;
            }

            // Determine which invoice to recalc
            var invoiceId = TryGetInvoiceId(ctx, trace);
            if (invoiceId == Guid.Empty)
            {
                trace.Trace("CalculateInvoiceTotalPlugin: No invoiceId found; exiting.");
                return;
            }

            // Sum line items
            var total = CalculateInvoiceTotal(svc, trace, invoiceId);

            // Update invoice total
            var invoice = new WHa_Invoice { Id = invoiceId };
            invoice.wha_totalamount = new Money(total);

            svc.Update(invoice);

            trace.Trace($"CalculateInvoiceTotalPlugin: Updated wha_invoice({invoiceId}) wha_totalamount={total:0.00}");
        }

        private static Guid TryGetInvoiceId(IPluginExecutionContext ctx, ITracingService trace)
        {
            // If triggered from invoice create: primary entity is invoice
            if (string.Equals(ctx.PrimaryEntityName, WHa_Invoice.EntityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                if (ctx.InputParameters.TryGetValue("Target", out var obj) && obj is Entity target)
                {
                    return target.Id;
                }

                return Guid.Empty;
            }

            // If triggered from invoice line item create/update/delete
            if (ctx.InputParameters.TryGetValue("Target", out var targetObj))
            {
                // CREATE/UPDATE: Target is Entity, but only includes changed columns (especially on Update)
                if (targetObj is Entity li)
                {
                    var er = li.GetAttributeValue<EntityReference>("wha_invoiceid");
                    if (er != null)
                    {
                        return er.Id;
                    }

                    // Fallback: on Update, invoiceid often isn't in Target, so use PreImage
                    if (ctx.PreEntityImages != null &&
                        ctx.PreEntityImages.TryGetValue("PreImage", out var pre) &&
                        pre != null)
                    {
                        var preEr = pre.GetAttributeValue<EntityReference>("wha_invoiceid");
                        if (preEr != null)
                        {
                            return preEr.Id;
                        }
                    }
                }

                // DELETE: Target is EntityReference (no attributes) so must use PreImage
                if (targetObj is EntityReference)
                {
                    if (ctx.PreEntityImages != null &&
                        ctx.PreEntityImages.TryGetValue("PreImage", out var pre) &&
                        pre != null)
                    {
                        var er = pre.GetAttributeValue<EntityReference>("wha_invoiceid");
                        if (er != null)
                        {
                            return er.Id;
                        }
                    }
                }
            }

            trace.Trace("TryGetInvoiceId: Could not determine invoiceId.");
            return Guid.Empty;
        }


        private static decimal CalculateInvoiceTotal(IOrganizationService svc, ITracingService trace, Guid invoiceId)
        {
            var qe = new QueryExpression(WHa_InvoiceLineItem.EntityLogicalName)
            {
                ColumnSet = new ColumnSet("wha_totallineitemamount", "wha_sourceid"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            qe.Criteria.AddCondition("wha_invoiceid", ConditionOperator.Equal, invoiceId);

            var results = svc.RetrieveMultiple(qe);

            decimal total = 0m;

            foreach (var e in results.Entities)
            {
                var amt = e.GetAttributeValue<Money>("wha_totallineitemamount")?.Value ?? 0m;

                if (IsDiscount(e))
                {
                    total -= amt;
                }
                else
                {
                    total += amt;
                }
            }

            trace.Trace($"CalculateInvoiceTotal: invoiceId={invoiceId}, lineItems={results.Entities.Count}, total={total:0.00}");
            return total;
        }

        private static bool IsDiscount(Entity lineItem)
        {
            var src = lineItem.GetAttributeValue<EntityReference>("wha_sourceid");
            if (src == null)
            {
                return false;
            }

            return string.Equals(src.LogicalName, "wha_discount", StringComparison.OrdinalIgnoreCase);
        }

    }
}
