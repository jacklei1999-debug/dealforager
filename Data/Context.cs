using Microsoft.EntityFrameworkCore;

namespace WebUIMVC.Data;

public class Context : DbContext
{
	public DbSet<Product> Products { get; set; }

	public Context(DbContextOptions<Context> options)
		: base(options)
	{
	}

	public Context()
	{
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		if (!optionsBuilder.IsConfigured)
		{
			optionsBuilder.UseSqlite("Data Source=DealA.db");
		}
	}
}
