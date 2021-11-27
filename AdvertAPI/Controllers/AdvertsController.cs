using AdvertAPI.Models;
using AdvertAPI.Models.Messages;
using AdvertAPI.Services;
using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdvertApi.Controllers
{
    [ApiController]
    [Route("adverts/v1")]
    [Produces("application/json")]
    public class AdvertsController : ControllerBase
    {
        private readonly IAdvertStorageService _advertStorageService;

        public AdvertsController(IAdvertStorageService advertStorageService, IConfiguration configuration)
        {
            _advertStorageService = advertStorageService;
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        [HttpPost]
        [Route("Create")]
        [ProducesResponseType(404)]
        [ProducesResponseType(201, Type = typeof(CreateAdvertResponse))]
        public async Task<IActionResult> Create(AdvertModel model)
        {
            string recordId;
            try
            {
                recordId = await _advertStorageService.Add(model);
            }
            catch (KeyNotFoundException)
            {
                return new NotFoundResult();
            }
            catch (Exception exception)
            {
                return StatusCode(500, exception.Message);
            }

            return StatusCode(201, new CreateAdvertResponse { Id = recordId });
        }

        [HttpPut]
        [Route("Confirm")]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Confirm(ConfirmAdvertModel model)
        {
            try
            {
                await _advertStorageService.Confirm(model);
                await RaiseAdvertConfirmedMessage(model);
            }
            catch (KeyNotFoundException)
            {
                return new NotFoundResult();
            }
            catch (Exception exception)
            {
                return StatusCode(500, exception.Message);
            }

            return new OkResult();
        }

        private async Task RaiseAdvertConfirmedMessage(ConfirmAdvertModel model)
        {
            var topicArn = Configuration.GetValue<string>("TopicArn");
            var dbModel = await _advertStorageService.GetById(model.Id);

            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                var message = new AdvertConfirmedMessage
                {
                    Id = model.Id,
                    Title = dbModel.Title
                };

                var messageJson = JsonConvert.SerializeObject(message);
                await client.PublishAsync(topicArn, messageJson);
            }
        }

        [HttpGet]
        [Route("{id}")]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Get(string id)
        {
            try
            {
                var advert = await _advertStorageService.GetById(id);
                return new JsonResult(advert);
            }
            catch (KeyNotFoundException)
            {
                return new NotFoundResult();
            }
            catch (Exception)
            {
                return new StatusCodeResult(500);
            }
        }

        [HttpGet]
        [Route("all")]
        [ProducesResponseType(200)]
        [EnableCors("AllOrigin")]
        public async Task<IActionResult> All()
        {
            return new JsonResult(await _advertStorageService.GetAllAsync());
        }
    }
}