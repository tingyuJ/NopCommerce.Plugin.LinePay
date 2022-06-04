using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.LinePay.Models
{

    public record ConfirmResponse
    {
        public string returnCode { get; set; }
        public string returnMessage { get; set; }
        public ConfirmInfo info { get; set; }
    }

    public record ConfirmInfo
    {
        public string orderId { get; set; }
        public long transactionId { get; set; }
        public string authorizationExpireDate { get; set; }
        public string regKey { get; set; }
        public List<ConfirmPayInfo> payInfo { get; set; }
        public List<ConfirmPackages> packages { get; set; }

        //TODO: 還有一堆merchantReference跟shipping的欄位不會用到先跳過
    }

    public record ConfirmPayInfo
    {
        public string method { get; set; }
        public int amount { get; set; }
        public string creditCardNickname { get; set; }
        public string creditCardBrand { get; set; }
        public string maskedCreditCardNumber { get; set; }
    }

    public class ConfirmPackages
    {
        public string id { get; set; }
        public int amount { get; set; }
        public int userFeeAmount { get; set; }
    }

}
