using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class EmployeeController : ApiController
    {
        // GET: api/Employee
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Employee/5
        public string Get(int id)
        {
            return "value";
        }

        [HttpPost]
        // POST: api/Employee
        public HttpResponseMessage Post([ModelBinder(typeof(FieldValueModelBinder))]Employee emp)
        {
            if (!ModelState.IsValid)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, "Please provide valid input");
            }
            else
                //Add Employee logic here
                return Request.CreateResponse(HttpStatusCode.OK, "Employee added sucessfully");
        }

        // PUT: api/Employee/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Employee/5
        public void Delete(int id)
        {
        }
    }
}
