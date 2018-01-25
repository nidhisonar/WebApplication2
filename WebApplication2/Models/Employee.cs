using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.ModelBinding;

namespace WebApplication2.Models
{
    [ModelBinder(typeof(FieldValueModelBinder))]
    public class Employee
    {
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string City { get; set; }
    }
}