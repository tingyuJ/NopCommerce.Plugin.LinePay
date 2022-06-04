using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.LinePay.Models
{
    public record RefundRequest
    {
        public decimal? refundAmount { get; set; }
    }

    public record RefundResponse
    {
        public string returnCode { get; set; }
        public string returnMessage { get; set; }
        public RefundInfo info { get; set; }
    }

    public record RefundInfo
    {
        public long refundTransactionId { get; set; }
        public string refundTransactionDate { get; set; }
    }
}
