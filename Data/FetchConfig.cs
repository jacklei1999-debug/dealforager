namespace WebUIMVC.Data;

public class FetchConfig
{
	public string MinSavings { get; set; } = string.Empty;

	public int Interval { get; set; }

	public string Category { get; set; } = string.Empty;

	public string DisplayName => $"[{MinSavings}%/{Interval}s]";
}
