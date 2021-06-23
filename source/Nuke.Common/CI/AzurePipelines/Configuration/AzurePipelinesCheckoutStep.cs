using JetBrains.Annotations;
using Nuke.Common.Utilities;

namespace Nuke.Common.CI.AzurePipelines.Configuration
{
    public enum AzurePipelinesCheckoutType
    {
        Self, None
    }
    
    [PublicAPI]
    public class AzurePipelinesCheckoutStep : AzurePipelinesStep
    {
        public AzurePipelinesCheckoutType CheckoutType { get; }
        public bool Submodules { get; }

        public AzurePipelinesCheckoutStep(AzurePipelinesCheckoutType checkoutType, bool submodules = false)
        {
            CheckoutType = checkoutType;
            Submodules = submodules;
        }

        public override void Write(CustomFileWriter writer)
        {
            using (writer.WriteBlock($"- checkout: {CheckoutType.ToString().ToLower()}"))
            {
                if (Submodules)
                {
                    writer.WriteLine("submodules: true");
                }
            }
        }
    }
}
