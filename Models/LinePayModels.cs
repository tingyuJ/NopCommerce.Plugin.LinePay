using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.LinePay.Models
{
    public record LinePayRequest  
    {
        public int amount { get; set; }

        public string currency { get; set; }

        public string orderId { get; set; }

        public List<Package> packages { get; set; }

        public Options options { get; set; }

        public Redirecturls redirectUrls { get; set; }
    }

    public record Package  
    {
        public string id { get; set; }

        public int amount { get; set; }

        public string name { get; set; }

        public List<Product> products { get; set; }
    }

    public record Product  
    {
        public string id { get; set; }

        public string name { get; set; }

        public string imageUrl { get; set; }

        public int quantity { get; set; }

        public int price { get; set; }
    }

    public record Options  
    {
        public Payment payment { get; set; }
        public Display display { get; set; }
    }

    //是否自動請款
    //true(預設)：呼叫Confirm API，統一進行授權/請款處理
    //false：呼叫Confirm API只能完成授權，需要呼叫Capture API完成請款
    public record Payment  
    {
        public bool capture { get; set; }
    }

    //等待付款頁的語言程式碼，預設為英文（en）
    //支援語言：en、ja、ko、th、zh_TW、zh_CN
    public record Display
    {
        public string locale { get; set; }
    }

    public record Redirecturls  
    {
        public string confirmUrl { get; set; }

        public string cancelUrl { get; set; }
    }

    public record LinePayResponse  
    {
        public string returnCode { get; set; }

        public string returnMessage { get; set; }

        public Info info { get; set; }
    }
    public record Info  
    {
        public Paymenturl paymentUrl { get; set; }

        public long transactionId { get; set; }

        public string paymentAccessToken { get; set; }
    }

    public record Paymenturl  
    {
        public string web { get; set; }

        public string app { get; set; }
    }

}
