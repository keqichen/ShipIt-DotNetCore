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
            //Improve truck 
            //Calculate the weight of each order;
            //Step 1.for each order, we calculate the whole weight of a same produce; 
            //e.g.10 apples, 10*5=50kg;

            //Step 2.rules: we keep adding new products to one truck until the weight > 2000kg;
            //  if first product's total weight <2000kg (add for loop whether other product is below (2000-first product weight)kg), assign one truck;

            //find some way to get trucks as int;
            double trucks = 0; 
            float totalWeight=0;

            Dictionary<int,float> productTotalWeight = new Dictionary<int, float>();
            var items = new List<int>();
            Dictionary<int,items> trucksWithProducts = new Dictionary<int, items>();
        //    [truck1:items1; truck2: items2]
            foreach(var products in lineItems){
                productTotalWeight.Add(products.ProductId,products.Weight*products.Quantity);
            }
               // [a:2001, b:500, c:500 ,d:1900]
            for(int i = 0; i < productTotalWeight.Count; i++)
            {
                KeyValuePair<int, string> item = productTotalWeight.ElementAt(i);

                for(int j = 1; j < productTotalWeight.Count; j++)
                {
                    KeyValuePair<int, string> item = productTotalWeight.ElementAt(j);

                    if(item[i].Value > 2000){
                        
                        items.Add(item[i]);

                        trucks = 1;
                        //take this as truck index;
                        var remainingWeight = totalWeight%2000;
                        

                        trucksWithProducts.Add(trucks,items);
                    }

                    
                
                    if(item[i].Value <= 2000 && item[j].Value <= 2000-item[i].Value){

                        items.Add(item[i]);

                    }
                }
            }

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