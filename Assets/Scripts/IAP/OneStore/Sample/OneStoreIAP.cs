using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Gaa;
using UnityEngine.Purchasing;
using ProductType = Gaa.ProductType;

/// <summary>
/// OneStore 内购接入：
/// https://dev.onestore.co.kr/devpoc/reference/view/Apps
/// </summary>
public class OneStoreIAP : MonoSingleton<OneStoreIAP>
{
    private static readonly string TAG = "OneStore";

    [TextAreaAttribute] public string base64EncodedPublicKey = "";

    private List<ProductDetail> productDetails;

    private Dictionary<string, PurchaseData> purchaseMap = new Dictionary<string, PurchaseData>();
    private Dictionary<string, string> signatureMap = new Dictionary<string, string>();

    public event Action<ProductData> OnPurchaseSuccessEvent;
    public event Action<int> OnPurchaseFailedEvent;

    enum PurchaseButtonState
    {
        NONE,
        ACKNOWLEDGE,
        CONSUME,
        REACTIVE,
        CANCEL
    };

    void AddListener()
    {
        GaaIapResultListener.PurchaseClientStateResponse += PurchaseClientStateResponse;
        GaaIapResultListener.OnPurchaseUpdatedResponse += OnPurchaseUpdatedResponse;
        GaaIapResultListener.OnPurchasesFailed += OnPurchasFailed;
        GaaIapResultListener.OnQueryPurchasesResponse += OnQueryPurchasesResponse;
        GaaIapResultListener.OnProductDetailsResponse += OnProductDetailsResponse;

        GaaIapResultListener.OnConsumeSuccessResponse += OnConsumeSuccessResponse;
        GaaIapResultListener.OnAcknowledgeSuccessResponse += OnAcknowledgeSuccessResponse;
        GaaIapResultListener.OnManageRecurringResponse += OnManageRecurringResponse;

        GaaIapResultListener.SendLog += SendLog;
    }

    void RemoveListener()
    {
        GaaIapResultListener.PurchaseClientStateResponse -= PurchaseClientStateResponse;
        GaaIapResultListener.OnPurchaseUpdatedResponse -= OnPurchaseUpdatedResponse;
        GaaIapResultListener.OnQueryPurchasesResponse -= OnQueryPurchasesResponse;
        GaaIapResultListener.OnProductDetailsResponse -= OnProductDetailsResponse;

        GaaIapResultListener.OnConsumeSuccessResponse -= OnConsumeSuccessResponse;
        GaaIapResultListener.OnAcknowledgeSuccessResponse -= OnAcknowledgeSuccessResponse;
        GaaIapResultListener.OnManageRecurringResponse -= OnManageRecurringResponse;

        GaaIapResultListener.SendLog -= SendLog;

        GaaIapCallManager.Destroy();
    }

    void OnDestroy()
    {
        RemoveListener();
    }

    public void Initialize(string[] products)
    {
        AddListener();

        StartCoroutine(StartConnectService());
    }

    PurchaseData GetPurchaseData(string productId)
    {
        PurchaseData pData = null;
        foreach (KeyValuePair<string, PurchaseData> pair in purchaseMap)
        {
            if (productId.Equals(pair.Key))
            {
                pData = pair.Value;
                break;
            }
        }

        return pData;
    }

    public void SendLog(string tag, string message)
    {
        Debug.Log("[" + tag + "]: " + message);
    }

    // ======================================================================================
    // Request
    // ======================================================================================

    IEnumerator StartConnectService()
    {
        yield return new WaitForSeconds(1.0f);
        StartConnection();
    }

    public void StartConnection()
    {
        SendLog(TAG, "StartConnection()");
        if (GaaIapCallManager.IsServiceAvailable() == false)
        {
            GaaIapCallManager.StartConnection(base64EncodedPublicKey);
        }
        else
        {
            SendLog(TAG, "StartConnection: Already connected to the payment module.");
        }
    }

    public void BuyProduct(string productId, string type = ProductType.INAPP)
    {
        SendLog(TAG, "BuyProduct - productId: " + productId + ", type: " + type);

        PurchaseFlowParams param = new PurchaseFlowParams();
        param.productId = productId;
        param.productType = type;
        //param.productName = "";
        //param.devPayload = "your Developer Payload";
        //param.gameUserId = "";
        //param.promotionApplicable = false;

        GaaIapCallManager.LaunchPurchaseFlow(param);
    }

    void QueryPurchases()
    {
        purchaseMap.Clear();
        signatureMap.Clear();

        GaaIapCallManager.QueryPurchases(ProductType.INAPP);
        GaaIapCallManager.QueryPurchases(ProductType.AUTO);
    }

    //对于消耗性产品，请使用GaaIapCallManager.Consume()进行确认购买
    void ConsumePurchase(string productId)
    {
        SendLog(TAG, "ConsumePurchase: productId: " + productId);
        PurchaseData purchaseData = GetPurchaseData(productId);
        if (purchaseData != null)
        {
            GaaIapCallManager.Consume(purchaseData, /*developerPayload*/null);
        }
        else
        {
            SendLog(TAG, "ConsumePurchase: purchase data is null.");
        }
    }

    //非消耗性产品，请使用 GaaIapCallManager.Acknowledge()进行确认购买
    void AcknowledgePurchase(string productId)
    {
        SendLog(TAG, "AcknowledgePurchase: productId: " + productId);
        PurchaseData purchaseData = GetPurchaseData(productId);
        if (purchaseData != null)
        {
            GaaIapCallManager.Acknowledge(purchaseData, /*developerPayload*/null);
        }
        else
        {
            SendLog(TAG, "AcknowledgePurchase: purchase data is null.");
        }
    }

    void ManageRecurringProduct(string productId)
    {
        SendLog(TAG, "ManageRecurringProduct: productId: " + productId);
        PurchaseData purchaseData = GetPurchaseData(productId);
        if (purchaseData != null)
        {
            string recurringAction = RecurringAction.REACTIVATE;
            if (purchaseData.recurringState == RecurringState.RECURRING)
            {
                recurringAction = RecurringAction.CANCEL;
            }

            GaaIapCallManager.ManageRecurringProduct(purchaseData, recurringAction);
        }
        else
        {
            SendLog(TAG, "ManageRecurringProduct: purchase data is null.");
        }
    }


    // ======================================================================================
    // Response
    // ======================================================================================

    void PurchaseClientStateResponse(IapResult iapResult)
    {
        SendLog(TAG, "PurchaseClientStateResponse:\n\t\t-> " + iapResult.ToString());
        if (iapResult.IsSuccess())
        {
            QueryPurchases();
            GaaIapCallManager.QueryProductDetails(null, ProductType.ALL);
        }
        else
        {
            GaaIapResultListener.HandleError("PurchaseClientStateResponse", iapResult);
        }
    }

    /// <summary>
    /// 购买成功
    /// </summary>
    void OnPurchaseUpdatedResponse(List<PurchaseData> purchases, List<string> signatures)
    {
        ParsePurchaseData("OnPurchaseUpdatedResponse", purchases, signatures);
    }

    void OnPurchasFailed(IapResult result)
    {
        OnPurchaseFailedEvent.Invoke(result.code);
    }

    /// <summary>
    /// 查询成功
    /// </summary>
    void OnQueryPurchasesResponse(List<PurchaseData> purchases, List<string> signatures)
    {
        ParsePurchaseData("OnQueryPurchasesResponse", purchases, signatures);
    }

    private void ParsePurchaseData(string func, List<PurchaseData> purchases, List<string> signatures)
    {
        SendLog(TAG, func);
        for (int i = 0; i < purchases.Count; i++)
        {
            PurchaseData p = purchases[i];
            string s = signatures[i];

            purchaseMap.Add(p.productId, p);
            signatureMap.Add(p.productId, s);

            OnPurchaseItemClick(p.productId, PurchaseButtonState.CONSUME);
        }
    }

    void OnPurchaseItemClick(string productId, PurchaseButtonState state)
    {
        SendLog(TAG, "OnPurchaseItemClick:\n\t\t-> productId: " + productId + ", state: " + state.ToString());
        switch (state)
        {
            case PurchaseButtonState.ACKNOWLEDGE:
                AcknowledgePurchase(productId);
                break;
            case PurchaseButtonState.CONSUME:
                ConsumePurchase(productId);
                break;
            case PurchaseButtonState.REACTIVE:
            case PurchaseButtonState.CANCEL:
                ManageRecurringProduct(productId);
                break;
        }
    }

    void OnProductDetailsResponse(List<ProductDetail> products)
    {
        SendLog(TAG, "OnProductDetailsResponse()");
        productDetails = products;

        foreach (ProductDetail detail in productDetails)
        {
            SendLog(TAG, "ProductDetail: " + detail.title);
        }
    }

    void OnConsumeSuccessResponse(PurchaseData purchaseData)
    {
        SendLog(TAG, "OnConsumeSuccessResponse:\n\t\t-> productId: " + purchaseData.productId);
        purchaseMap.Remove(purchaseData.productId);
        signatureMap.Remove(purchaseData.productId);

        var pdata = ProductData.FromProduct(purchaseData);
        OnPurchaseSuccessEvent?.Invoke(pdata);
    }

    void OnAcknowledgeSuccessResponse(PurchaseData purchaseData)
    {
        SendLog(TAG, "OnAcknowledgeSuccessResponse:\n\t\t-> productId: " + purchaseData.productId);
        QueryPurchases();
    }

    void OnManageRecurringResponse(PurchaseData purchaseData, string action)
    {
        SendLog(TAG, "OnManageRecurringResponse:\n\t\t-> productId: " + purchaseData.productId + ", action: " + action);
        QueryPurchases();
    }
}