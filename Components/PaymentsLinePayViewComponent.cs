using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.LinePay.Components
{
    [ViewComponent(Name = "PaymentsLinePay")]
    public class PaymentsLinePayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.LinePay/Views/PaymentInfo.cshtml");
        }
    }
}
