using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.LinePay.Models;
using Nop.Plugin.Payments.LinePay.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.DataTables;
using Nop.Web.Framework.Mvc.Filters;
using Org.BouncyCastle.Crypto.Tls;

namespace Nop.Plugin.Payments.LinePay.Controllers
{
    [AutoValidateAntiforgeryToken]
    [AuthorizeAdmin] //confirms access to the admin panel
    [Area(AreaNames.Admin)] //specifies the area containing a controller or action
    public class LinePayPluginController : BasePaymentController
    {
        #region Fields

        private readonly Services.LinePay _service;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IOrderService _orderService;
        private readonly IPictureService _pictureService;

        #endregion

        #region Ctor

        public LinePayPluginController(Services.LinePay service,
            ISettingService settingService,
            IStoreContext storeContext,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            IOrderService orderService,
            IPictureService pictureService)
        {
            _service = service;
            _settingService = settingService;
            _storeContext = storeContext;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _orderService = orderService;
            _pictureService = pictureService;
        }

        #endregion

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var linePaySettings = await _settingService.LoadSettingAsync<LinePaySettings>(storeScope);

            var model = new ConfigurationModel
            {
                ChannelId = linePaySettings.ChannelId,
                ChannelSecretKey = linePaySettings.ChannelSecretKey,
                ActiveStoreScopeConfiguration = storeScope,
                PictureId = linePaySettings.PictureId
            };

            if (storeScope <= 0)
                return View("~/Plugins/Payments.LinePay/Views/Configure.cshtml", model);

            model.ChannelId_OverrideForStore = await _settingService.SettingExistsAsync(linePaySettings, x => x.ChannelId, storeScope);
            model.ChannelSecretKey_OverrideForStore = await _settingService.SettingExistsAsync(linePaySettings, x => x.ChannelSecretKey, storeScope);
            model.PictureId_OverrideForStore = await _settingService.SettingExistsAsync(linePaySettings, x => x.PictureId, storeScope);

            return View("~/Plugins/Payments.LinePay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var linePaySettings = await _settingService.LoadSettingAsync<LinePaySettings>(storeScope);
            var previousPicture = linePaySettings.PictureId;

            //save settings
            linePaySettings.ChannelId = model.ChannelId;
            linePaySettings.ChannelSecretKey = model.ChannelSecretKey;
            linePaySettings.PictureId = model.PictureId;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(linePaySettings, x => x.ChannelId, model.ChannelId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(linePaySettings, x => x.ChannelSecretKey, model.ChannelSecretKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(linePaySettings, x => x.PictureId, model.PictureId_OverrideForStore, storeScope, false);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            var currentPicture = linePaySettings.PictureId;

            //delete an old picture (if deleted or updated)
            if (previousPicture != currentPicture)
            {
                var oldPicture = await _pictureService.GetPictureByIdAsync(previousPicture);
                if (oldPicture != null)
                    await _pictureService.DeletePictureAsync(oldPicture);
            }
            
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpGet]
        public async Task<IActionResult> Confirm([FromQuery] string orderId, [FromQuery] string transactionId)
        {
            try
            {
                var order = await _orderService.GetOrderByGuidAsync(Guid.Parse(orderId));
                order.AuthorizationTransactionId = transactionId;

                var confirm = await _service.ConfirmLinePayAsync(orderId, transactionId);

                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = $"Payment.LinePay Response: {confirm.returnMessage}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                if (confirm.returnCode != "0000")
                {
                    return Content(confirm.returnMessage);
                }

                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = OrderStatus.Processing;
                order.PaidDateUtc = DateTime.UtcNow;
                await _orderService.UpdateOrderAsync(order);

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        #endregion

    }
}
