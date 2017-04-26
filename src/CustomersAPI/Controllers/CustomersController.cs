﻿// Licensed under the MIT license. See LICENSE file in the samples root for full license information.

using CustomerAPI.Data;
using CustomersShared.Data.DataEntities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Resources;
using System.Threading.Tasks;

namespace CustomerAPI.Controllers
{
    [Route("api/[controller]")]
    public class CustomersController : Controller
    {
        private readonly ICustomersDataProvider _customersDataProvider;
        private readonly ResourceManager _resourceManager;
        private readonly ILogger _logger;

        // Dependency Injection: ASP.NET Core will automatically populate controller constructor arguments by resolving
        // services from the DI container. If needed, those objects will be created by calling constructors
        // whose own arguments will be provided by DI, and so on recursively until the whole object graph
        // needed has been constructed.
        // Logging: Here a logger is dependency injected and then used to log to the loggers added to the LoggerFactory.
        //          The category of CustomersController has been added using the <T> of CustomersController. This information
        //          will be written out in the log information. The logging information is written throughout the methods below.
        public CustomersController(ICustomersDataProvider customersDataProvider,
                                   ResourceManager resourceManager,
                                   ILogger<CustomersController> logger)
        {
            _customersDataProvider = customersDataProvider;
            _resourceManager = resourceManager;
            _logger = logger;
        }

        // GET: api/Customers
        [HttpGet]
        public IEnumerable<CustomerEntity> Get()
        {
            // Logging: Messages are logged using the logger that was dependency-injected. This call logs information
            //          to all the loggers added into the LoggerFactory.
            _logger.LogInformation(BuildLogInfo(nameof(Get),  "LoggingGetCustomers"));
            return _customersDataProvider.GetCustomers();
        }

        // GET api/Customers/5
        [HttpGet("{id}")]
        public ObjectResult Get(Guid id)
        {
            _logger.LogInformation(BuildLogInfo(nameof(Get), "LoggingGetCustomer", id));

            var customerDataActionResult = _customersDataProvider.TryFindCustomer(id);

            if (!customerDataActionResult.IsSuccess)
            {
                // Dependency Injection: HttpContext.RequestService can be used to resolve depdency-injected services
                // But receiving them via constructor injection is preferred.
                var resourceManager = HttpContext.RequestServices.GetService(typeof(ResourceManager)) as ResourceManager;

                // Logging: Here is an example of logging an error
                _logger.LogError(BuildLogInfo(nameof(Get), "CustomerNotFound", id));
                return new NotFoundObjectResult(BuildStringFromResource("CustomerNotFound", id));
            }

            return Ok(customerDataActionResult.CustomerEntity);
        }

        // POST api/Customers
        // Dependency Injection: using [FromService] is another way of requesting services from DI
        [HttpPost]
        public async Task<ObjectResult> PostAsync([FromBody]CustomerDataTransferObject customerDataTransferObject,
                                                  [FromServices] ResourceManager resourceManager)
        {
            if (customerDataTransferObject == null || !customerDataTransferObject.ValidateCustomerDataTransferObject())
            {
                _logger.LogError(BuildLogInfo(nameof(PostAsync), "CustomerInfoInvalid", resourceManager));
                return BadRequest(BuildStringFromResource("CustomerInfoInvalid", resourceManager));
            }

            var customerName = $"{customerDataTransferObject.FirstName} {customerDataTransferObject.LastName}";

            _logger.LogInformation(BuildLogInfo(nameof(PostAsync), "LoggingAddingCustomer", resourceManager, customerName));
            var customerDataActionResult = await _customersDataProvider.TryAddCustomerAsync(customerDataTransferObject);

            if (!customerDataActionResult.IsSuccess)
            {
                _logger.LogError(BuildLogInfo(nameof(PostAsync), "UnexpectedServerError", resourceManager));
                return StatusCode(StatusCodes.Status500InternalServerError, BuildStringFromResource("UnexpectedServerError", resourceManager));
            }

            _logger.LogInformation(BuildLogInfo(nameof(PostAsync), "LoggingAddedCustomer", resourceManager, customerName));
            return Ok(customerDataActionResult.CustomerEntity);
        }

        // PUT api/Customers/5
        [HttpPut("{id}")]
        public async Task<ObjectResult> PutAsync(Guid id, [FromBody]CustomerDataTransferObject customerDataTransferObject)
        {
            if (customerDataTransferObject == null || !customerDataTransferObject.ValidateCustomerDataTransferObject())
            {
                _logger.LogError(BuildLogInfo(nameof(PutAsync), "CustomerInfoInvalid"));
                return BadRequest(BuildStringFromResource("CustomerInfoInvalid"));
            }

            _logger.LogInformation(BuildLogInfo(nameof(PutAsync), "LoggingUpdatingCustomer", id));

            if (!_customersDataProvider.CustomerExists(id))
            {
                _logger.LogError(BuildLogInfo(nameof(PutAsync), "CustomerNotFound", id));
                return new NotFoundObjectResult(BuildStringFromResource("CustomerNotFound", id));
            }

            var customerDataActionResult = await _customersDataProvider.TryUpdateCustomerAsync(id, customerDataTransferObject);

            if (!customerDataActionResult.IsSuccess)
            {
                _logger.LogError(BuildLogInfo(nameof(PutAsync), "UnexpectedServerError"));
                return StatusCode(StatusCodes.Status500InternalServerError, BuildStringFromResource("UnexpectedServerError"));
            }

            _logger.LogInformation(BuildLogInfo(nameof(PutAsync), "LoggingUpdatedCustomer", id));
            return Ok(customerDataActionResult.CustomerEntity);
        }

        // DELETE api/Customers/5
        [HttpDelete("{id}")]
        public async Task<ObjectResult> DeleteAsync(Guid id)
        {
            _logger.LogInformation(BuildLogInfo(nameof(DeleteAsync), "LoggingDeletingCustomer", id));

            if (!_customersDataProvider.CustomerExists(id))
            {
                _logger.LogError(BuildLogInfo(nameof(DeleteAsync), "CustomerNotFound", id));
                return new NotFoundObjectResult(BuildStringFromResource("CustomerNotFound", id));
            }

            var customerDataActionResult = await _customersDataProvider.TryDeleteCustomerAsync(id);
            if (!customerDataActionResult.IsSuccess)
            {
                _logger.LogError(BuildLogInfo(nameof(DeleteAsync), "UnexpectedServerError"));
                return StatusCode(StatusCodes.Status500InternalServerError, BuildStringFromResource("UnexpectedServerError"));
            }

            _logger.LogInformation(BuildLogInfo(nameof(DeleteAsync), "LoggingDeletedCustomer", id));
            return Ok(customerDataActionResult.CustomerEntity);
        }

        /// <summary>
        /// Builds up a string looking up a resource and doing the replacements
        /// </summary>
        /// <param name="resourceStringName">Name of resource to use</param>
        /// <param name="replacements">Strings to use for replacing in the resource string</param>
        private string BuildStringFromResource(string resourceStringName, params object[] replacements)
        {
            return BuildStringFromResource(resourceStringName, _resourceManager, replacements);
        }

        /// <summary>
        /// Builds up a string looking up a resource and doing the replacements
        /// </summary>
        /// <param name="resourceStringName">Name of resource to use</param>
        /// <param name="resourceManager">ResourceManager instance to look up resources with</param>
        /// <param name="replacements">Strings to use for replacing in the resource string</param>
        private string BuildStringFromResource(string resourceStringName, ResourceManager resourceManager, params object[] replacements)
        {
            return string.Format(resourceManager.GetString(resourceStringName), replacements);
        }

        /// <summary>
        /// Builds up a log string using the parameters passed in
        /// </summary>
        /// <param name="methodName">Name of method logging from</param>
        /// <param name="resourceStringName">Name of resource to use</param>
        /// <param name="replacements">Strings to use for replacing in the resource string</param>
        private string BuildLogInfo(string methodName, string resourceStringName, params object[] replacements)
        {
            return BuildLogInfo(methodName, resourceStringName, _resourceManager, replacements);
        }

        /// <summary>
        /// Builds up a log string using the parameters passed in
        /// </summary>
        /// <param name="methodName">Name of method logging from</param>
        /// <param name="resourceStringName">Name of resource to use</param>
        /// <param name="resourceManager">ResourceManager instance to use for looking up resources</param>
        /// <param name="replacements">Strings to use for replacing in the resource string</param>
        private string BuildLogInfo(string methodName, string resourceStringName, ResourceManager resourceManager, params object[] replacements)
        {
            return $"{methodName}: {BuildStringFromResource(resourceStringName, resourceManager, replacements)}";
        }
    }
}
