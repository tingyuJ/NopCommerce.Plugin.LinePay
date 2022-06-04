using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper.Internal;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Net.Http.Headers;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.LinePay.Infrastructure.Cache;
using Nop.Plugin.Payments.LinePay.Models;
using Nop.Plugin.Payments.LinePay.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Factories;

namespace Nop.Plugin.Payments.LinePay.Services
{
    public class LinePay
    {
        #region Fields

        private readonly HttpClient _httpClient;
        private readonly LinePaySettings _linePaySettings;
        private readonly IOrderService _orderService;
        private readonly ICommonModelFactory _commonModelFactory;
        private readonly IStoreService _storeService;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly IWebHelper _webHelper;
        private readonly IPictureService _pictureService;

        #endregion

        #region Ctor

        public LinePay(HttpClient client,
            LinePaySettings linePaySettings,
            IOrderService orderService,
            ICommonModelFactory commonModelFactory,
            IStoreService storeService,
            IStaticCacheManager staticCacheManager,
            IWebHelper webHelper,
            IPictureService pictureService)
        {
            //configure client
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CURRENT_VERSION}");

            _httpClient = client;
            _linePaySettings = linePaySettings;
            _orderService = orderService;
            _commonModelFactory = commonModelFactory;
            _storeService = storeService;
            _staticCacheManager = staticCacheManager;
            _webHelper = webHelper;
            _pictureService = pictureService;
        }

        #endregion

        #region Utilities

        public async Task<string> SendRequestAsync(string apiUrl, Order order, object requestBody)
        {
            using (var httpClient = new HttpClient())
            {
                //Settings
                var channelId = _linePaySettings.ChannelId;
                var channelSecret = _linePaySettings.ChannelSecretKey;
                //var channelId = "1657146343";
                //var channelSecret = "a8f55c90dfd0e5394ea0f63a4f601fe3";
                var baseUri = "https://sandbox-api-pay.line.me";
                var orderId = order.OrderGuid.ToString();
                
                var body = JsonSerializer.SerializeToElement(requestBody).ToString();

                string Signature = HashLinePayRequest(channelSecret, apiUrl, body, orderId, channelSecret);

                httpClient.DefaultRequestHeaders.Add("X-LINE-ChannelId", channelId);
                httpClient.DefaultRequestHeaders.Add("X-LINE-Authorization-Nonce", orderId);
                httpClient.DefaultRequestHeaders.Add("X-LINE-Authorization", Signature);

                var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(baseUri + apiUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                return result;
            }
        }

        internal static string HashLinePayRequest(string channelSecret, string apiUrl, string body, string orderId, string key)
        {
            var request = channelSecret + apiUrl + body + orderId;
            key = key ?? "";

            var encoding = new System.Text.UTF8Encoding();
            byte[] keyByte = encoding.GetBytes(key);
            byte[] messageBytes = encoding.GetBytes(request);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashmessage);
            }
        }

        protected async Task<string> GetPictureUrlAsync(int pictureId)
        {
            var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(ModelCacheEventConsumer.PICTURE_URL_MODEL_KEY,
                pictureId, _webHelper.IsCurrentConnectionSecured() ? Uri.UriSchemeHttps : Uri.UriSchemeHttp);

            return await _staticCacheManager.GetAsync(cacheKey, async () =>
            {
                //little hack here. nulls aren't cacheable so set it to ""
                var url = await _pictureService.GetPictureUrlAsync(pictureId, showDefaultPicture: false) ?? "";
                return url;
            });
        }

        #endregion

        #region Methods

        public async Task<LinePayResponse> RequestLinePayAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var orderId = postProcessPaymentRequest.Order.OrderGuid.ToString();
            var amount = (int)postProcessPaymentRequest.Order.OrderTotal;
            var storeName = (await _storeService.GetStoreByIdAsync(postProcessPaymentRequest.Order.StoreId)).Name;

            var requestBody = new LinePayRequest()
            {
                options = new Options()
                {
                    payment = new Payment()
                    {
                        capture = true
                    }
                },
                amount = amount,
                currency = "TWD",
                orderId = orderId,
                packages = new List<Package>()
                    {
                        //Products
                        new Package()
                        {
                            id = "package-1",
                            name = "name-1",
                            amount = amount,
                            products = new List<Product>()
                            {
                                new Product()
                                {
                                    id = "nop-" + postProcessPaymentRequest.Order.Id.ToString(),
                                    name = storeName,
                                    //TODO:
                                    imageUrl =  await GetPictureUrlAsync(_linePaySettings.PictureId),
                                    quantity = 1,
                                    price = amount
                                }
                            }
                        }
                    },
                redirectUrls = new Redirecturls()
                {
                    confirmUrl = "https://localhost:44369/Admin/LinePayPlugin/Confirm",
                    cancelUrl = "https://localhost:44369/" //TODO
                }
            };

            var result = await SendRequestAsync("/v3/payments/request", postProcessPaymentRequest.Order, requestBody);

            return JsonSerializer.Deserialize<LinePayResponse>(result);

        }

        public async Task<ConfirmResponse> ConfirmLinePayAsync(string orderId, string transactionId)
        {
            var order = await _orderService.GetOrderByGuidAsync(Guid.Parse(orderId));
            var amount = (int)order.OrderTotal;

            var requestBody = new LinePayRequest()
            {
                amount = amount,
                currency = "TWD"
            };

            var result = await SendRequestAsync($"/v3/{transactionId}/confirm", order, requestBody);

            return JsonSerializer.Deserialize<ConfirmResponse>(result);
        }

        public async Task<string> RefundAsync(LinePaySettings settings, Order order, decimal? amount = null)
        {
            var requestBody = new RefundRequest()
            {
                refundAmount = amount
            };

            var result = await SendRequestAsync($"/v3/payments//{order.AuthorizationTransactionId}/refund", order, requestBody);
            var resultJson = JsonSerializer.Deserialize<RefundResponse>(result);

            //紀錄退款結果
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = $"Payment.LinePay Refund Response: {resultJson.returnMessage}; Refund Transaction ID: {resultJson.info.refundTransactionId}.",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            if (resultJson.returnCode != "0000")
                return resultJson.returnMessage;

            return string.Empty;
        }

        #endregion
    }
}
