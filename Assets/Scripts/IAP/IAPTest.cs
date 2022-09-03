using SDK;
using UnityEngine;
using UnityEngine.UI;

namespace IAP
{
    public class IAPTest : MonoBehaviour
    {
        public Transform ListContent;
        public GameObject payItem;

        private string[] all_products =
        {
            "pay_50dia",
            "pay_260dia",
            "pay_480dia",
            "pay_980dia",
            "pay_1980dia",
            "pay_3280dia",
            "pay_4680dia",
            "pay_6480dia",
            "pay_month"
        };

        // Start is called before the first frame update
        void Start()
        {
            AddProductsUI();

#if ONE_STORE
        OneStoreIAP.Instance.Initialize(all_products);
        OneStoreIAP.Instance.OnPurchaseSuccessEvent += (product) => { Debug.Log("购买成功:" + product.id); };
        OneStoreIAP.Instance.OnPurchaseFailedEvent += (error) => { Debug.Log("购买失败:" + error); };
#else
            UnityIAP.Instance.Initialize(all_products);
            UnityIAP.Instance.OnPurchaseSuccessEvent += (product) => { Debug.Log("购买成功:" + product.id); };
            UnityIAP.Instance.OnPurchaseFailedEvent += (error, product) => { Debug.Log("购买失败:" + error); };
#endif
        }

        void AddProductsUI()
        {
            foreach (var product_id in all_products)
            {
                GameObject go = Instantiate(payItem, ListContent);
                go.SetActive(true);
                go.transform.Find("Text").GetComponent<Text>().text = product_id;
                go.transform.Find("Button").GetComponent<Button>().onClick.AddListener(() => { OnBuyItem(product_id); });
            }
        }

        void OnBuyItem(string product_id)
        {
#if ONE_STORE
        OneStoreIAP.Instance.BuyProduct(product_id);
#else
            UnityIAP.Instance.BuyProduct(product_id);
#endif
        }
    }
}