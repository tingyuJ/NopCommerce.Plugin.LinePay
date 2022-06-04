using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.LinePay
{
    /// <summary>
    /// Represents settings of the PayPal Standard payment plugin
    /// </summary>
    public class LinePaySettings : ISettings
    {
        /// <summary>
        /// Gets or sets Channel ID	
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        /// Gets or sets Channel Secret Key	
        /// </summary>
        public string ChannelSecretKey { get; set; }

        public int PictureId { get; set; }

    }
}
