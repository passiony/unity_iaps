using System;
using System.Collections.Generic;
using System.Text;
using LitJson;
using UnityEngine;
using UnityEngine.Purchasing;

namespace SDK
{
// 从 IStoreListener 派生 Purchaser 类使其能够接收来自 Unity Purchasing 的消息。
    public class UnityIAP : MonoSingleton<UnityIAP>, IStoreListener
    {

        const string BASE_URL = "";
#if UNITY_IOS
        private string VerifyURL = BASE_URL + "/charge";
#elif UNITY_ANDROID
        private string VerifyURL = BASE_URL + "/gp_charge";
#else
        private string VerifyURL = BASE_URL + "/charge";
#endif

        public event Action<Product[]> OnInitializedEvent;
        public event Action<int> OnInitializeFailedEvent;
        public event Action<ProductData> OnPurchaseSuccessEvent;
        public event Action<int, ProductData> OnPurchaseFailedEvent;

        private IStoreController m_StoreController;         // Unity 采购系统
        private IExtensionProvider m_StoreExtensionProvider; // 商店特定的采购子系统.
        private string purchasingProductId;                //正在支付支付中的productId
        
        private const string PendingPrefs = "PendingPrefs";
        private Dictionary<string, ProductData> pendingProducts = new Dictionary<string, ProductData>();
    
        public void Initialize(string[] all_products)
        {
            if (IsInitialized())
            {
                return;
            }

            // Create a builder, first passing in a suite of Unity provided stores.
            var module = StandardPurchasingModule.Instance();
            module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
            
            var builder = ConfigurationBuilder.Instance(module);
            
            //添加商品列表
            var products = new List<ProductDefinition>();
            foreach (var id in all_products)
            {
                products.Add(new ProductDefinition(id, ProductType.Consumable));
            }
            builder.AddProducts(products);
            
            //调用 UnityPurchasing.Initialize 方法可启动初始化过程，从而提供监听器的实现和配置。
            //请注意，如果网络不可用，初始化不会失败；Unity IAP 将继续尝试在后台初始化。仅在 Unity IAP 遇到无法恢复的问题（例如配置错误或在设备设置中禁用 IAP）时，初始化才会失败。
            //因此，Unity IAP 所需的初始化时间量可能是任意的；如果用户处于飞行模式，则会是无限期的时间。您应该相应地设计您的应用商店，防止用户在初始化未成功完成时尝试购物。
            UnityPurchasing.Initialize(this, builder);

            InitPendingOrder();
        }

        private bool IsInitialized()
        {
            // Only say we are initialized if both the Purchasing references are set.
            return m_StoreController != null && m_StoreExtensionProvider != null;
        }

        // Notice how we use the general product identifier in spite of this ID being mapped to
        // custom store-specific identifiers above.
        public void BuyProduct(string productId)
        {
            // If Purchasing has been initialized ...
            if (IsInitialized())
            {
                // ... look up the Product reference with the general product identifier and the Purchasing 
                // system's products collection.
                Product product = m_StoreController.products.WithID(productId);

                // If the look up found a product for this device's store and that product is ready to be sold ... 
                if (product != null && product.availableToPurchase)
                {
                    Debug.Log(string.Format("Purchasing product asychronously: '{0}'", product.definition.id));
                    // ... buy the product. Expect a response either through ProcessPurchase or OnPurchaseFailed asynchronously.
                    m_StoreController.InitiatePurchase(product);
                }
                // Otherwise ...
                else
                {
                    // ... report the product look-up failure situation  
                    Debug.Log(
                        "BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
                    OnPurchaseFailedEvent?.Invoke((int)PurchaseFailureReason.ProductUnavailable, ProductData.FromProduct(product));
                }
            }
            else
            {
                // ... report the fact Purchasing has not succeeded initializing yet. Consider waiting longer or 
                // retrying initiailization.
                Debug.Log("BuyProductID FAIL. Not initialized.");
                OnPurchaseFailedEvent?.Invoke((int)PurchaseFailureReason.PurchasingUnavailable,new ProductData(productId));
            }
        }

        public void CancelPurchase()
        {
            if (!string.IsNullOrEmpty(purchasingProductId))
            {
                Product product = m_StoreController.products.WithID(purchasingProductId);
                if (product != null && product.availableToPurchase)
                {
                    m_StoreController.ConfirmPendingPurchase(product);
                    purchasingProductId = null;
                }
            }
        }
        
        // Restore purchases previously made by this customer. Some platforms automatically restore purchases, like Google. 
        // Apple currently requires explicit purchase restoration for IAP, conditionally displaying a password prompt.
        public void RestorePurchases()
        {
            // If Purchasing has not yet been set up ...
            if (!IsInitialized())
            {
                // ... report the situation and stop restoring. Consider either waiting longer, or retrying initialization.
                Debug.Log("RestorePurchases FAIL. Not initialized.");
                OnPurchaseFailedEvent?.Invoke((int)PurchaseFailureReason.PurchasingUnavailable, new ProductData());
                return;
            }

            // If we are running on an Apple device ... 
            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                // ... begin restoring purchases
                Debug.Log("RestorePurchases started ...");

                // Fetch the Apple store-specific subsystem.
                var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
                // Begin the asynchronous process of restoring purchases. Expect a confirmation response in 
                // the Action<bool> below, and ProcessPurchase if there are previously purchased products to restore.
                apple.RestoreTransactions((result) =>
                {
                    // The first phase of restoration. If no more responses are received on ProcessPurchase then 
                    // no purchases are available to be restored.
                    Debug.Log("RestorePurchases continuing: " + result +
                              ". If no further messages, no purchases available to restore.");
                });
            }
            // Otherwise ...
            else
            {
                // We are not running on an Apple device. No work is necessary to restore purchases.
                Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
            }
        }


        #region IStoreListener

        /// <summary>
        /// 初始化成功
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="extensions"></param>
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            // Purchasing has succeeded initializing. Collect our Purchasing references.
            Debug.Log("OnInitialized: PASS");

            // Overall Purchasing system, configured with products for this application.
            m_StoreController = controller;
            // Store specific subsystem, for accessing device-specific store features.
            m_StoreExtensionProvider = extensions;

            OnInitializedEvent?.Invoke(controller.products.all);

#if LOGGER_ON
            StringBuilder sb = new StringBuilder();
            sb.Append("内购列表展示 -> count:" + controller.products.all.Length + "\n");
            foreach (var item in controller.products.all)
            {
                if (item.availableToPurchase)
                {
                    sb.Append("localizedPriceString :" + item.metadata.localizedPriceString + "\n" +
                              "localizedTitle :" + item.metadata.localizedTitle + "\n" +
                              "localizedDescription :" + item.metadata.localizedDescription + "\n" +
                              "isoCurrencyCode :" + item.metadata.isoCurrencyCode + "\n" +
                              "localizedPrice :" + item.metadata.localizedPrice + "\n" +
                              "type :" + item.definition.type + "\n" +
                              "receipt :" + item.receipt + "\n" +
                              "enabled :" + (item.definition.enabled ? "enabled" : "disabled") + "\n \n");
                }
            }
            Debug.Log(sb.ToString());
#endif
        }

        /// <summary>
        /// 初始化失败
        /// </summary>
        /// <param name="error"></param>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            // Purchasing set-up has not succeeded. Check error for reason. Consider sharing this reason with the user.
            Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
            purchasingProductId = null;
            OnInitializeFailedEvent?.Invoke((int) error);
        }

        /// <summary>
        /// 购买成功
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            purchasingProductId = null;
            var pdata = ProductData.FromProduct(args.purchasedProduct);
            
            // A product has been purchased by this user.
            OnPurchaseSuccessEvent?.Invoke(pdata);

            AddPendingOrder(pdata);
            // Return a flag indicating whether this product has completely been received, or if the application needs 
            // to be reminded of this purchase at next app launch. Use PurchaseProcessingResult.Pending when still 
            // saving purchased products to the cloud, and when that save is delayed. 
            return PurchaseProcessingResult.Complete;
        }

        /// <summary>
        /// 购买失败
        /// </summary>
        /// <param name="product"></param>
        /// <param name="failureReason"></param>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            // A product purchase attempt did not succeed. Check failureReason for more detail. Consider sharing 
            // this reason with the user to guide their troubleshooting actions.
            Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}",
                product.definition.storeSpecificId, failureReason));

            OnPurchaseFailedEvent?.Invoke((int) failureReason, ProductData.FromProduct(product));
        }

        #endregion

        #region 二次验证

        /// <summary>
        /// 初始化未完成订单
        /// </summary>
        private void InitPendingOrder()
        {
            string json = PlayerPrefs.GetString(PendingPrefs);
            if (!string.IsNullOrEmpty(json))
            {
                pendingProducts = JsonMapper.ToObject<Dictionary<string, ProductData>>(json);
                Debug.Log("[IAP] InitPendingOrder:" + json);
                Debug.Log("[IAP] pendingProducts.Count:" + pendingProducts.Count);
            }
        }

        /// <summary>
        /// 添加未完成订单，等待服务器验证
        /// </summary>
        /// <param name="product"></param>
        private void AddPendingOrder(ProductData product)
        {
            if (!pendingProducts.ContainsKey(product.transactionID))
            {
                pendingProducts.Add(product.transactionID, product);
                string json = JsonMapper.ToJson(pendingProducts);
                PlayerPrefs.SetString(PendingPrefs, json);
            }
        }

        /// <summary>
        /// 已完成验证的删除订单
        /// </summary>
        /// <param name="product"></param>
        private bool RemovePendingOrder(ProductData product)
        {
            if (pendingProducts.ContainsKey(product.transactionID))
            {
                pendingProducts.Remove(product.transactionID);
                string json = JsonMapper.ToJson(pendingProducts);
                PlayerPrefs.SetString(PendingPrefs, json);
                return true;
            }

            return false;
        }


        public int GetPendingOrderCount()
        {
            return pendingProducts.Count;
        }
        
        /// <summary>
        /// 通知server进行订单收据的二次验证
        /// </summary>
        /// <param name="userID">用户id</param>
        /// <param name="product">商品</param>
        /// <param name="callback">回调</param>
        public void ReceiptVerify(string userID, ProductData product, Action<int> callback)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("userID", userID);
            dic.Add("receipt", product.receipt);

            string args = JsonMapper.ToJson(dic);
            Debug.LogWarning($"[IAP.Req]: {VerifyURL}?{args}");

            NetworkHttp.Instance.Post(VerifyURL, null, ByteUtility.StringToBytes(args), (response) =>
            {
                if (response == null)
                {
                    callback?.Invoke(408);
                    return;
                }

                var result = JsonMapper.ToObject(response);
                if (result == null || !result.ContainsKey("code"))
                {
                    callback?.Invoke(408);
                    Debug.LogError("[IAP.Verify] with err : result is null!");
                    return;
                }
                
                var code = Convert.ToInt32(result["code"].ToString());
                if (code != 0)
                {
                    callback?.Invoke(code);
                    Debug.LogErrorFormat("[IAP.Verify] with err : {0}", result["msg"]);
                    return;
                }

                var exit = RemovePendingOrder(product);
                if (!exit)
                {
                    callback?.Invoke(409);
                    return;
                }
                
                callback?.Invoke(200);
            }, 15);
        }

        /// <summary>
        /// 检测待办订单列表
        /// </summary>
        /// <param name="userID">用户id</param>
        /// <param name="callback">回调</param>
        public void CheckPendingOrder(string userID, Action<int, ProductData> callback)
        {
            foreach (var pair in pendingProducts)
            {
                ReceiptVerify(userID, pair.Value, code => { callback?.Invoke(code, pair.Value); });
                break;
            }
        }

        #endregion
    }
}