﻿using System;
using System.Collections.Generic;
using System.Linq;
 using Microsoft.AspNetCore.Mvc;
 using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Repositories;

namespace ShipIt.Controllers
{
    [Route("orders/outbound")]
    public class OutboundOrderController : ControllerBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        private readonly IStockRepository _stockRepository;
        private readonly IProductRepository _productRepository;

        public OutboundOrderController(IStockRepository stockRepository, IProductRepository productRepository)
        {
            _stockRepository = stockRepository;
            _productRepository = productRepository;
        }

        [HttpPost("")]
        public double Post([FromBody] OutboundOrderRequestModel request)
        {
            Log.Info(String.Format("Processing outbound order: {0}", request));

            var gtins = new List<String>();
            foreach (var orderLine in request.OrderLines)
            {
                if (gtins.Contains(orderLine.gtin))
                {
                    throw new ValidationException(String.Format("Outbound order request contains duplicate product gtin: {0}", orderLine.gtin));
                }
                gtins.Add(orderLine.gtin);//list of Product Ids from the Order
            }
            
            var productDataModels = _productRepository.GetProductsByGtin(gtins);
            var products = productDataModels.ToDictionary(p => p.Gtin, p => new Product(p));

            var lineItems = new List<StockAlteration>();

            var productIds = new List<int>();
            var errors = new List<string>();

            //check whether a product is valid;
            foreach (var orderLine in request.OrderLines)
            {
                if (!products.ContainsKey(orderLine.gtin))
                {
                    errors.Add(string.Format("Unknown product gtin: {0}", orderLine.gtin));
                }
                else
                {
                    var product = products[orderLine.gtin];
                    // a list of valid products in the order;
                    //We are passing product weight;
                    lineItems.Add(new StockAlteration(product.Id,orderLine.quantity,product.Weight));
                    productIds.Add(product.Id);
                }
            }

            if (errors.Count > 0)
            {
                throw new NoSuchEntityException(string.Join("; ", errors));
            }

            var stock = _stockRepository.GetStockByWarehouseAndProductIds(request.WarehouseId, productIds);

            var orderLines = request.OrderLines.ToList();
            errors = new List<string>();

            //check whether the product is in stock;
            //they don't unpdate lineItems;
            for (int i = 0; i < lineItems.Count; i++)
            {
                
                var lineItem = lineItems[i];
                var orderLine = orderLines[i];

                if (!stock.ContainsKey(lineItem.ProductId))
                {
                    errors.Add(string.Format("Product: {0}, no stock held", orderLine.gtin));
                    continue;
                }

                var item = stock[lineItem.ProductId];
                if (lineItem.Quantity > item.held)
                {
                    errors.Add(
                        string.Format("Product: {0}, stock held: {1}, stock to remove: {2}", orderLine.gtin, item.held,
                            lineItem.Quantity));
                }
            }
            
            // double trucks = 0; 
            // float totalWeight=0;

            // Dictionary<int,float> productTotalWeight = new Dictionary<int, float>();
            // var items = new List<int>();
            // Dictionary<int,items> trucksWithProducts = new Dictionary<int, items>();

        
            // foreach(var products in lineItems){
            //     productTotalWeight.Add(products.ProductId,products.Weight*products.Quantity);
            // }
            
            // checkTrucksRequired(productTotalWeight);

            // Dictionary<int,items> checkTrucksRequired(Dictionary<int,float> productTotalWeight){
            //     foreach(var totalvalue in productTotalWeight)
            //     {
            //             totalWeight =+ totalvalue.Value;
            //     }
            //     //sort dictionary;
            //     var sortedDict = from entry in productTotalWeight orderby entry.Value descending select entry;

            //     for(int i = 0; i < sortedDict.Count; i++)
            //     {
            //         KeyValuePair<int, string> item = sortedDict.ElementAt(i);

            //         //when the whole order can fit in one truck;
            //         if(totalWeight <= 2000){
            //                 trucks = 1;
            //                 items.Add(item[i]);
            //                 trucksWithProducts.Add(trucks,items);
            //         }else{
                        
            //         //if we need more than one truck, iterate each item;
            //             if(item[i]>2000){
            //                 //this is the truck where we should put this item in;
            //                 trucks = Math.Floor(item[i].Value/2000);
            //                 items.Add(item[i]);
            //                 trucksWithProducts.Add(trucks,items);
                            
            //                 var remainingWeight = item[i].Value-2000*trucks;

            //                 //replace the value of this heavy item with the remaining weight;
            //                 item[i].Value= remainingWeight;

            //                 checkTrucksRequired(items);  
            //             }
            //             else{
            //                 // for now it's not perfect, if one truck has 1900, then it's fine;
            //                 // we load another truck;
            //                 var remainingCapacity = 2000 - item[i].Value;
            //                 trucks = 1;
            //                 items.Add(item[i]);
                            
            //                 for(int j = 1; j < sortedDict.Count; j++){
            //                     KeyValuePair<int, string> product = sortedDict.ElementAt(j);
                                
            //                     if(product[j].Value <= remainingCapacity)
            //                     {
            //                         items.Add(product[j]);
            //                         remainingCapacity = 2000 - (item[i].Value + product[j].Value);
            //                     }else{
            //                         trucksWithProducts.Add(trucks,items);
            //                         trucks = 1;
            //                         items.Add(product[j]);
            //                     }
            //                 }
            //                 trucksWithProducts.Add(trucks,items);
            //             }

            //         }
            //     }

            //     return trucksWithProducts;
            // }


            // Calculate truck number;
            float totalWeight = 0;
            double trucks = 0;
            if(lineItems.Count!=0){
                foreach(var items in lineItems)
                {
                    totalWeight += items.Weight * items.Quantity;
                }
                //how to get rid of math.ceiling?
                trucks = Math.Ceiling(totalWeight/2000);
            }

            if (errors.Count > 0)
            {
                throw new InsufficientStockException(string.Join("; ", errors));
            }

            _stockRepository.RemoveStock(request.WarehouseId, lineItems);

            return trucks;
        }


    }

}
