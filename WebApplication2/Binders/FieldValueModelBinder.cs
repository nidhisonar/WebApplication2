using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Web.Http.Controllers;
using System.Web;

/// <summary>
/// This class will be called before binding received data in API to Model class 
/// And validate Attributes and convert data according to its property in Model class
/// </summary>
public class FieldValueModelBinder : System.Web.Http.ModelBinding.IModelBinder
{
    /// <summary>
    /// Store received data in API in KeyValuePair
    /// </summary>
    private List<KeyValuePair<string, string>> kvps;

    /// <summary>
    /// Storing error while binding data in Model class
    /// </summary>
    private Dictionary<string, string> dictionaryErrors = new Dictionary<string, string>();

    /// <summary>
    /// Implementing Base method and binding received data in API to its respected property in Model class
    /// </summary>
    /// <param name="actionContext">Http Action Context</param>
    /// <param name="bindingContext">Model Binding Context</param>
    /// <returns>True if no error while binding. False if any error occurs during model binding</returns>
    public bool BindModel(HttpActionContext actionContext, System.Web.Http.ModelBinding.ModelBindingContext bindingContext)
    {
        try
        {
            var bodyString = actionContext.Request.Content.ReadAsStringAsync().Result;
            if (actionContext.Request.Method.Method.ToUpper().Equals("GET"))
            {
                var uriContext = HttpUtility.ParseQueryString(actionContext.Request.RequestUri.Query);
                if (uriContext.HasKeys())
                {
                    this.kvps = uriContext.AllKeys.ToDictionary(k => k, k => uriContext[k]).ToList<KeyValuePair<string, string>>();
                }
            }
            else if (!string.IsNullOrEmpty(bodyString))
            {
                this.kvps = this.ConvertToKvps(bodyString);
            }
            else
            {
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Please provide valid input data.");
                return false;
            }
        }
        catch (Exception ex)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Please provide data in a valid format.");
            return false;
        }

        // Initiate primary object
        var obj = Activator.CreateInstance(bindingContext.ModelType);
        try
        {
            this.SetPropertyValues(obj);
        }
        catch (Exception ex)
        {
            if (this.dictionaryErrors.Any())
            {
                foreach (KeyValuePair<string, string> keyValuePair in this.dictionaryErrors)
                {
                    bindingContext.ModelState.AddModelError(keyValuePair.Key, keyValuePair.Value);
                }
            }
            else
            {
                bindingContext.ModelState.AddModelError("Internal Error", ex.Message);
            }

            this.dictionaryErrors.Clear();
            return false;
        }

        // Assign completed Mapped object to Model
        bindingContext.Model = obj;
        return true;
    }

    /// <summary>
    /// Sets the property value from received data in API to instance of Model class
    /// </summary>
    /// <param name="obj">Instance of Model class to which data needs to be store</param>
    public void SetPropertyValues(object obj)
    {
        // Recursively set PropertyInfo array for object hierarchy
        PropertyInfo[] props = obj.GetType().GetProperties();

        // Set KV Work List for real iteration process so that kvps is not in iteration and
        // its items from kvps can be removed after each iteration
        List<KeyValuePair<string, string>> kvpsWork = new List<KeyValuePair<string, string>>(this.kvps);

        this.CheckForMandatoryField(props, this.kvps);

        foreach (var item in kvpsWork)
        {
            var key = item.Key;

            PropertyInfo prop = props.FirstOrDefault(x => x.Name.ToLower() == key.ToLower());
            if (prop == null)
            {
                this.dictionaryErrors.Add(key, $"{key} is not a valid parameter.");
                continue;
            }

            if (key.ToLower() == prop.Name.ToLower())
            {
                // Populate KeyValueWork and pass it for adding property to object
                var kvw = new KeyValueWork()
                {
                    Key = item.Key,
                    Value = item.Value,
                    SourceKvp = item
                };

                this.AddSingleProperty(obj, prop, kvw);
            }
        }

        if (this.dictionaryErrors.Count > 0)
        {
            throw new Exception();
        }
    }

    /// <summary>
    /// Checking for mandatory fields in a Class properties
    /// </summary>
    /// <param name="props">Array of Properties in class</param>
    /// <param name="obj">Variable having data to be mapped in Model in Key Value Pair form</param>
    private void CheckForMandatoryField(PropertyInfo[] props, List<KeyValuePair<string, string>> obj)
    {
        var mandatoryProp = props.Where(m => m.CustomAttributes.Any());
        foreach (PropertyInfo parameter in mandatoryProp)
        {
            var validationAttributes = parameter.CustomAttributes;

            foreach (var attributeData in validationAttributes)
            {
                var attributeInstance = parameter.GetCustomAttribute(attributeData.AttributeType);

                var validationAttribute = attributeInstance as ValidationAttribute;

                if (validationAttribute != null)
                {
                    var arg = obj.Where(x => x.Key.ToLower() == parameter.Name.ToLower()).Select(x => x.Value).FirstOrDefault();
                    var isValid = validationAttribute.IsValid(arg);
                    if (!isValid)
                    {
                        this.dictionaryErrors.Add(parameter.Name, validationAttribute.FormatErrorMessage(parameter.Name));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Convert a single value according to it's data type in Model class
    /// </summary>
    /// <param name="obj">It is an instance of Model class </param>
    /// <param name="prop">Property to which we want to convert and store</param>
    /// <param name="item">Variable having actual data which needs to be converted</param>
    private void AddSingleProperty(object obj, PropertyInfo prop, KeyValueWork item)
    {
        try
        {
            // Check is Property is Nullable or not
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    // Get the actual property
                    var type = Nullable.GetUnderlyingType(prop.PropertyType);
                    if (type == typeof(Guid))
                    {
                        var value = Guid.Parse(item.Value);
                        prop.SetValue(obj, value, null);
                    }
                    else
                    {
                        prop.SetValue(obj, Convert.ChangeType(item.Value, prop.PropertyType.GetGenericArguments()[0]), null);
                    }
                }
            }
            else
            {
                // Set value for non-nullable property 
                prop.SetValue(obj, Convert.ChangeType(item.Value, prop.PropertyType), null);
            }

            this.kvps.Remove(item.SourceKvp);
        }
        catch (Exception e)
        {
            this.dictionaryErrors.Add(prop.Name, $"Please provide a valid input for {prop.Name}");
        }
    }

    /// <summary>
    /// Convert data received by API to KeyValuePair
    /// </summary>
    /// <param name="sourceString">Raw data which is received by API</param>
    /// <returns>Returns received string in KeyValuePair</returns>
    private List<KeyValuePair<string, string>> ConvertToKvps(string sourceString)
    {
        // Converting object to json and storing in key value pair
        System.Web.Script.Serialization.JavaScriptSerializer jsonSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        Dictionary<string, string> item = jsonSerializer.Deserialize<Dictionary<string, string>>(sourceString);

        List<KeyValuePair<string, string>> kvpList = item.ToList<KeyValuePair<string, string>>();

        return kvpList;
    }

    /// <summary>
    /// Class to store data while Parsing received data in Model property
    /// </summary>
    private class KeyValueWork
    {
        /// <summary>
        /// Gets or sets Hold Name of the property
        /// </summary>
        internal string Key { get; set; }

        /// <summary>
        /// Gets or sets the value of received data
        /// </summary>
        internal string Value { get; set; }

        /// <summary>
        /// Gets or sets data in VariableName, VariableData format
        /// </summary>
        internal KeyValuePair<string, string> SourceKvp { get; set; }
    }
}