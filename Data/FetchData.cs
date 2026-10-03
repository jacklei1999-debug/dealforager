using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebUIMVC.Data;

public class FetchData
{
	public async Task<List<Product>> GetProducts(string cat, string sorttype, string miniSaving)
	{
		using Context myContext = new Context();
		using HttpClient client = new HttpClient();
		client.DefaultRequestHeaders.TryAddWithoutValidation("Content-type", "application/json");
				Product? lastProduct = null;
		bool theEnd = false;
		int NewAddedProducts = 0;
		while (!theEnd)
		{
			string url;
			if (lastProduct == null)
			{
				url = "https://dealforager.com/api/products?cat=" + cat + "&domain=1&sort=" + sorttype + "&minSavings=" + miniSaving;
			}
			else
			{
				string a = lastProduct.asin;
				string p = lastProduct.pricedifference.ToString();
				string s = lastProduct.savingspercent.ToString();
				string d = lastProduct.dealscore.ToString();
				string u = lastProduct.usedprice.ToString();
				url = "https://dealforager.com/api/products?cat=" + cat + "&domain=1&sort=" + sorttype + "&minSavings=" + miniSaving + "&a=" + a + "&p=" + p + "&s=" + s + "&d=" + d + "&u=" + u;
				Console.WriteLine(url);
			}
			var Products_Results_Obj = Newtonsoft.Json.Linq.JArray.Parse(await client.GetStringAsync(url));
			int ProductListSize = 0;
			Console.WriteLine("Pulled items: " + Products_Results_Obj.Count);
			if (Products_Results_Obj.Count > 0)
			{
				try
				{
					foreach (dynamic ii in Products_Results_Obj)
					{
						ProductListSize++;
						int[] asciiValues = ii.image.ToObject<int[]>();
						string imageUrl = "";
						if (asciiValues != null)
						{
							int[] array = asciiValues;
							for (int i = 0; i < array.Length; i++)
							{
								imageUrl += (char)array[i];
							}
						}
						else
						{
							imageUrl = "i";
						}
						Product Product_i = new Product();
						Product_i.asin = ii.asin;
						Product_i.imageURL = imageUrl;
						Product_i.dealscore = ii.dealscore;
						Product_i.pricedifference = ii.pricedifference;
						Product_i.title = "e";
						Product_i.updated_at = ii.updated_at;
						Product_i.savingspercent = ii.savingspercent;
						Product_i.usedprice = ii.usedprice;
						Product_i.intertTime = DateTime.Now;
						Product_i.rootcat = ii.rootcat;
						Product_i.readit = 1;
						Product_i.liked = 0;
						List<Product> ProductExist = myContext.Products.Where((Product product) => product.asin == Product_i.asin).ToList();
						if (ProductExist.Count() == 0)
						{
							Product_i.liked = 0;
							myContext.Products.Add(Product_i);
							NewAddedProducts++;
							Console.WriteLine("BrandNewItem");
						}
						else if (Product_i.savingspercent > 6000 && (Product_i.intertTime - ProductExist.First().intertTime).TotalDays > 5.0)
						{
							Product_i.liked = 1;
							myContext.Products.Remove(ProductExist.First());
							myContext.Products.Add(Product_i);
							NewAddedProducts++;
							Console.WriteLine("Old-60%OFFItem");
						}
						if (ProductListSize == Products_Results_Obj.Count)
						{
							lastProduct = Product_i;
							myContext.SaveChanges();
							Console.WriteLine(ProductListSize);
							Console.WriteLine("Saving");
						}
					}
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Console.WriteLine(ex2.ToString());
				}
				if (Products_Results_Obj.Count < 64)
				{
					theEnd = true;
				}
			}
			else
			{
				theEnd = true;
			}
		}
		Console.BackgroundColor = ConsoleColor.Blue;
		Console.WriteLine(DateTime.Now.ToString() + "  --  " + NewAddedProducts + " added");
		Console.BackgroundColor = ConsoleColor.Black;
		return myContext.Products.ToList();
	}
}
