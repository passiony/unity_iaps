using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gaa
{
    public class GaaIapResultListener : MonoBehaviour
    {
        private static string TAG = "GaaIapResultListener";

        private List<PurchaseData> purchases = new List<PurchaseData>();
        private List<string> signatures = new List<string>();

        private List<ProductDetail> products = new List<ProductDetail>();

        public static event Action<bool> OnLoadingVisibility;
        public static event Action<IapResult> PurchaseClientStateResponse;
        public static event Action<List<ProductDetail>> OnProductDetailsResponse;
        public static event Action<List<PurchaseData>, List<string>> OnPurchaseUpdatedResponse;
        public static event Action<List<PurchaseData>, List<string>> OnQueryPurchasesResponse;
        public static event Action<IapResult> OnPurchasesFailed;
        public static event Action<PurchaseData> OnConsumeSuccessResponse;
        public static event Action<PurchaseData> OnAcknowledgeSuccessResponse;
        public static event Action<PurchaseData, string> OnManageRecurringResponse;

        public static event Action<string, string> SendLog;

        void Awake()
        {
            GaaIapCallbackManager.PurchaseClientStateEvent += PurchaseClientStateEvent;

            GaaIapCallbackManager.PurchaseUpdatedSuccessEvent += PurchaseUpdatedSuccessEvent;
            GaaIapCallbackManager.PurchaseUpdatedErrorEvent += PurchaseUpdatedErrorEvent;

            GaaIapCallbackManager.QueryPurchasesSuccessEvent += QueryPurchasesSuccessEvent;
            GaaIapCallbackManager.QueryPurchasesErrorEvent += QueryPurchasesErrorEvent;

            GaaIapCallbackManager.ProductDetailsSuccessEvent += ProductDetailsSuccessEvent;
            GaaIapCallbackManager.ProductDetailsErrorEvent += ProductDetailsErrorEvent;

            GaaIapCallbackManager.ConsumeSuccessEvent += ConsumeSuccessEvent;
            GaaIapCallbackManager.ConsumeErrorEvent += ConsumeErrorEvent;

            GaaIapCallbackManager.AcknowledgeSuccessEvent += AcknowledgeSuccessEvent;
            GaaIapCallbackManager.AcknowledgeErrorEvent += AcknowledgeErrorEvent;

            GaaIapCallbackManager.RecurringSuccessEvent += RecurringSuccessEvent;
            GaaIapCallbackManager.RecurringErrorEvent += RecurringErrorEvent;

            GaaIapCallbackManager.StoreInfoEvent += StoreInfoEvent;

            GaaIapCallbackManager.LoginFlowEvent += LoginFlowEvent;
            GaaIapCallbackManager.UpdateOrInstallFlowEvent += UpdateOrInstallFlowEvent;
        }

        void OnDestroy()
        {
            GaaIapCallbackManager.PurchaseClientStateEvent -= PurchaseClientStateEvent;

            GaaIapCallbackManager.PurchaseUpdatedSuccessEvent -= PurchaseUpdatedSuccessEvent;
            GaaIapCallbackManager.PurchaseUpdatedErrorEvent -= PurchaseUpdatedErrorEvent;

            GaaIapCallbackManager.ProductDetailsSuccessEvent -= ProductDetailsSuccessEvent;
            GaaIapCallbackManager.ProductDetailsErrorEvent -= ProductDetailsErrorEvent;

            GaaIapCallbackManager.ConsumeSuccessEvent -= ConsumeSuccessEvent;
            GaaIapCallbackManager.ConsumeErrorEvent -= ConsumeErrorEvent;

            GaaIapCallbackManager.AcknowledgeSuccessEvent -= AcknowledgeSuccessEvent;
            GaaIapCallbackManager.AcknowledgeErrorEvent -= AcknowledgeErrorEvent;

            GaaIapCallbackManager.RecurringSuccessEvent -= RecurringSuccessEvent;
            GaaIapCallbackManager.RecurringErrorEvent -= RecurringErrorEvent;

            GaaIapCallbackManager.StoreInfoEvent -= StoreInfoEvent;

            GaaIapCallbackManager.LoginFlowEvent -= LoginFlowEvent;
            GaaIapCallbackManager.UpdateOrInstallFlowEvent -= UpdateOrInstallFlowEvent;
        }


        void PurchaseClientStateEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            PurchaseClientStateResponse?.Invoke(iapResult);
        }

        // ===========================================================================================

        /**
         * launchPurchaseFlow 如果购买成功，将调用
         */
        void PurchaseUpdatedSuccessEvent(PurchaseData purchaseData, string signature, int count, int totalCount)
        {
            // 관리형상품(inapp)의 경우 소비를 하지 않을 경우 재구매요청을 하여도 구매가 되지 않습니다.
            // 꼭, 소비(consume) 또는 확인(acknowledge) 과정을 통하여 소모성상품 소비 또는 확인을 진행해야 합니다.

            // 월정액상품(auto)의 경우 구매내역조회 시 recurringState 정보를 통하여 현재상태정보를 확인할 수 있습니다.
            // 참조) GaaPurchaseResponse.RecurringState
            // 월정액상품(auto)의 경우 확인(acknowledge) 과정을 진행해야 합니다.

            // 관리형상품의 소비(consume) 또는 확인(acknowledge), 월정액상품의 확인(acknowledge)
            // 을 하지 않으면 일정 기간후에 자동 '구매취소' 됩니다.
            if (purchaseData != null)
            {
                SendLog?.Invoke(TAG, "PurchaseUpdatedSuccessEvent\n\t\t-> count: " + count + ", totalCount: " + totalCount);
                if (count == 1)
                {
                    purchases.Clear();
                    signatures.Clear();
                }

                purchases.Add(purchaseData);
                signatures.Add(signature);

                if (count == totalCount)
                {
                    OnLoadingVisibility?.Invoke(false);
                    OnPurchaseUpdatedResponse?.Invoke(purchases, signatures);
                }
            }
            else
            {
                OnLoadingVisibility?.Invoke(false);
                SendLog?.Invoke(TAG, "PurchaseUpdatedSuccessEvent - no PurchaseData");
            }
        }
        /**
         * launchPurchaseFlow 如果购买失败，将调用。
         */
        void PurchaseUpdatedErrorEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            OnPurchasesFailed?.Invoke(iapResult);
            HandleError("PurchaseUpdatedErrorEvent", iapResult);
        }
        // ===========================================================================================


        /**
         * queryPurchases 如果查询成功，将调用未消费的购买信息。
         */
        void QueryPurchasesSuccessEvent(PurchaseData purchaseData, string signature, int count, int totalCount)
        {
            // 관리형상품(inapp)의 경우 소비를 하지 않을 경우 재구매요청을 하여도 구매가 되지 않습니다.
            // 꼭, 소비(consume) 또는 확인(acknowledge) 과정을 통하여 소모성상품 소비 또는 확인을 진행해야 합니다.

            // 월정액상품(auto)의 경우 구매내역조회 시 recurringState 정보를 통하여 현재상태정보를 확인할 수 있습니다.
            // 참조) GaaPurchaseResponse.RecurringState
            // 월정액상품(auto)의 경우 확인(acknowledge) 과정을 진행해야 합니다.

            // 관리형상품의 소비(consume) 또는 확인(acknowledge), 월정액상품의 확인(acknowledge)
            // 을 하지 않으면 일정 기간후에 자동 '구매취소' 됩니다.
            if (purchaseData != null)
            {
                SendLog?.Invoke(TAG, "QueryPurchasesSuccessEvent\n\t\t-> count: " + count + ", totalCount: " + totalCount);
                if (count == 1)
                {
                    purchases.Clear();
                    signatures.Clear();
                }

                purchases.Add(purchaseData);
                signatures.Add(signature);

                if (count == totalCount)
                {
                    OnLoadingVisibility?.Invoke(false);
                    OnQueryPurchasesResponse?.Invoke(purchases, signatures);
                }
            }
            else
            {
                OnLoadingVisibility?.Invoke(false);
                SendLog?.Invoke(TAG, "QueryPurchasesSuccessEvent - no PurchaseData");
            }
        }
        /**
         * queryPurchases 如果查询失败，将调用未消费的购买信息。
         */
        void QueryPurchasesErrorEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            OnPurchasesFailed?.Invoke(iapResult);
            HandleError("QueryPurchasesErrorEvent", iapResult);
        }
        // ===========================================================================================

        /**
         * InApp商品信息成功的回应。
         */
        void ProductDetailsSuccessEvent(ProductDetail productDetail, int count, int totalCount)
        {
            if (productDetail != null)
            {
                SendLog?.Invoke(TAG, "ProductDetailsSuccessEvent\n\t\t-> count: " + count + ", totalCount: " + totalCount);
                if (count == 1)
                {
                    products.Clear();
                }

                products.Add(productDetail);

                if (count == totalCount)
                {
                    products.Sort(delegate (ProductDetail a, ProductDetail b)
                    {
                        int price1 = int.Parse(a.price);
                        int price2 = int.Parse(b.price);
                        return price1.CompareTo(price2);
                    }
                    );

                    OnLoadingVisibility?.Invoke(false);
                    OnProductDetailsResponse?.Invoke(products);
                }
            } else
            {
                OnLoadingVisibility?.Invoke(false);
                SendLog?.Invoke(TAG, "ProductDetailsSuccessEvent - no ProductDetail");
            }
        }

        /**
         * IAP商品信息失败的回应。
         */
        void ProductDetailsErrorEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            HandleError("ProductDetailsErrorEvent", iapResult);
        }
        // ===========================================================================================


        /**
         * 消耗型产品（inapp）消费的成功响应函数。
         */
        void ConsumeSuccessEvent(PurchaseData purchaseData)
        {
            SendLog?.Invoke(TAG, "ConsumeSuccessEvent: " + purchaseData.ToString());
            OnLoadingVisibility?.Invoke(false);
            OnConsumeSuccessResponse?.Invoke(purchaseData);
        }

        /**
         * 관리형상품(inapp)의 소비에 대한 실패 응답 함수 입니다.
         */
        void ConsumeErrorEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            HandleError("ConsumeErrorEvent", iapResult);
        }
        // ===========================================================================================


        /**
         * 消耗性产品（inapp）或月度商品（auto）确认的成功响应函数。
         * 要确定最新状态，必须通过queryPurchases重新接收数据。
         */
        void AcknowledgeSuccessEvent(PurchaseData purchaseData)
        {
            SendLog?.Invoke(TAG, "AcknowledgeSuccessEvent: " + purchaseData.ToString());
            OnLoadingVisibility?.Invoke(false);
            OnAcknowledgeSuccessResponse?.Invoke(purchaseData);
        }

        /**
         * 관리형상품(inapp) 또는 월정액상품(auto)의 확인에 대한 실패 응답 함수 입니다.
         */
        void AcknowledgeErrorEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            HandleError("AcknowledgeErrorEvent", iapResult);
        }
        // ===========================================================================================


        /**
         * 월정액상품(auto) 상태변경에 대한 성공 응답 함수 입니다.
         * 최산 상태를 확인하려면 queryPurchases를 통해 다시 데이터를 받아와야 합니다.
         */
        void RecurringSuccessEvent(PurchaseData purchaseData, string action)
        {
            SendLog?.Invoke(TAG, "RecurringSuccessEvent: " + purchaseData.ToString());
            OnLoadingVisibility?.Invoke(false);
            OnManageRecurringResponse?.Invoke(purchaseData, action);
        }

        /**
         * 월정액상품(auto) 상태변경에 대한 실패 응답 함수 입니다.
         */
        void RecurringErrorEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            HandleError("AcknowledgeErrorEvent", iapResult);
        }
        // ===========================================================================================


        void StoreInfoEvent(string result)
        {
            OnLoadingVisibility?.Invoke(false);
            AndroidNative.ShowMessage("StoreInfoEvent", result, "ok");
        }
        // ===========================================================================================


        // 로그인에 대한 응답 함수 입니다.
        void LoginFlowEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            if (iapResult.IsSuccess())
            {
                AndroidNative.ShowMessage("LoginFlowEvent", iapResult.ToString(), "ok");
            }
            else
            {
                HandleError("LoginFlowEvent", iapResult);
            }
        }

        // 업데이트 또는 설치에 대한 응답 함수 입니다.
        void UpdateOrInstallFlowEvent(IapResult iapResult)
        {
            OnLoadingVisibility?.Invoke(false);
            if (iapResult.IsSuccess())
            {
                AndroidNative.ShowMessage("UpdateOrInstallFlowEvent", iapResult.ToString(), "ok");
            }
            else
            {
                HandleError("UpdateOrInstallFlowEvent", iapResult);
            }
        }
        // ===========================================================================================

        public static void HandleError(string tag, IapResult iapResult)
        {
            SendLog?.Invoke(TAG + "::" + tag, "\nHandleError: " + iapResult.ToString());
            if (iapResult.code == ResponseCode.RESULT_NEED_UPDATE)
            {
                // 원스토어 서비스 설치 or 업데이트 요청 진행
                SendLog?.Invoke(TAG, "launchUpdateOrInstallFlow start!");
                GaaIapCallManager.LaunchUpdateOrInstallFlow();
            }
            else if (iapResult.code == ResponseCode.RESULT_NEED_LOGIN)
            {
                // 원스토어 로그인을 요청하거나 원스토어 로그인이 필요함을 사용자에게 알림
                SendLog?.Invoke(TAG, "launchLoginFlow start!");
                GaaIapCallManager.LaunchLoginFlow();
            }
            else
            {
                // 기타 오류처리
                AndroidNative.ShowMessage(tag, "iapResult: " + iapResult.ToString(), "ok");
            }
        }
    }
}
