using Gaa;
using UnityEngine.Purchasing;

namespace UnityEngine.Purchasing
{
    public class ProductData
    {
        /// <summary>
        /// Store independent ID.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// The type of the product.
        /// </summary>
        public ProductType type { get; set; }

        /// <summary>
        /// A unique identifier for this product's transaction.
        ///
        /// This will only be set when the product was purchased during this session.
        /// </summary>
        public string transactionID { get; set; }

        /// <summary>
        /// The purchase receipt for this product, if owned.
        /// For consumable purchases, this will be the most recent purchase receipt.
        /// Consumable receipts are not saved between app restarts.
        /// Receipts is in JSON format.
        /// </summary>
        public string receipt { get; set; }


        public ProductData(){}

        public ProductData(string id)
        {
            this.id = id;
        }
        
        public static ProductData FromProduct(Product product)
        {
            var data = new ProductData();
            data.id = product.definition.id;
            data.type = product.definition.type;
            data.transactionID = product.transactionID;
            data.receipt = product.receipt;
             
            return data;
        }
        
        public static ProductData FromProduct(PurchaseData product)
        {
            var data = new ProductData();
            data.id = product.productId;
            data.type = ProductType.Consumable;
            data.transactionID = product.purchaseId;
            data.receipt = product.purchaseToken;
             
            return data;
        }
    }
}