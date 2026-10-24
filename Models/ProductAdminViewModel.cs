using System.Collections.Generic;
using WebUIMVC.Data;
using WebUIMVC.MyClasses;

namespace WebUIMVC.Models
{
    public class ProductAdminViewModel : Products
    {
        public int TotalCount { get; set; }
        public Product? OldestProduct { get; set; }
    }
}

