using System;

namespace WebUIMVC.Data;

public class Product
{
	public int ProductID { get; set; }

	public string asin { get; set; } = string.Empty;

	public int dealscore { get; set; }

	public int pricedifference { get; set; }

	public int savingspercent { get; set; }

	public string title { get; set; } = string.Empty;

	public DateTime updated_at { get; set; }

	public int usedprice { get; set; }

	public DateTime intertTime { get; set; }

	public int readit { get; set; }

	public int liked { get; set; }

	public string rootcat { get; set; } = string.Empty;

	public string imageURL { get; set; } = string.Empty;
}
