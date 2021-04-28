using ClosedXML.Excel;
using FileCreateWorkerService.Models;
using FileCreateWorkerService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileCreateWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly RabbitMQClientService _rabbitMQClientService;

        private readonly IServiceProvider _serviceProvider;

        private IModel _channel;
        public Worker(ILogger<Worker> logger,RabbitMQClientService rabbitMQClientService, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _rabbitMQClientService = rabbitMQClientService;
            _serviceProvider = serviceProvider;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {

         _channel=_rabbitMQClientService.Connect();
            _channel.BasicQos(0, 1, false);

            return base.StartAsync(cancellationToken);
        }

        protected override  Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var consumer = new AsyncEventingBasicConsumer(_channel);

            _channel.BasicConsume(RabbitMQClientService.QueueName, false, consumer);
          
            consumer.Received += Consumer_Received;

            return Task.CompletedTask;
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs @event)
        {
            await Task.Delay(5000);


            var createExcelMessage = JsonSerializer.Deserialize<CreateExcelMessage>(Encoding.UTF8.GetString(@event.Body.ToArray()));


           using var ms = new MemoryStream();


            var wb = new XLWorkbook();
            var ds = new DataSet();
            ds.Tables.Add(GetTable("products"));
;

            wb.Worksheets.Add(ds);
            wb.SaveAs(ms);

            MultipartFormDataContent multipartFormDataContent = new();

            multipartFormDataContent.Add(new ByteArrayContent(ms.ToArray()), "file", Guid.NewGuid().ToString() + ".xlsx");

            var baseUrl = "https://localhost:44321/api/files";

            using (var httpClient=  new HttpClient())
            {

                var response = await httpClient.PostAsync($"{baseUrl}?fileId={createExcelMessage.FileId}", multipartFormDataContent);

                if(response.IsSuccessStatusCode)
                {

                    _logger.LogInformation($"File ( Id : {createExcelMessage.FileId}) was created by successful");
                    _channel.BasicAck(@event.DeliveryTag, false);
                }
            }

        }

        private DataTable GetTable(string tableName)
        {
            List<FileCreateWorkerService.Models.Product> products;

            using (var scope= _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AdventureWorks2019Context>();

                products = context.Products.ToList();
            }

            DataTable table = new DataTable { TableName = tableName };

            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("Name", typeof(String));
            table.Columns.Add("ProductNumber", typeof(string));
            table.Columns.Add("Color", typeof(string));

            products.ForEach(x =>
            {
                table.Rows.Add(x.ProductId, x.Name, x.ProductNumber, x.Color);

            });

            return table;


        }
    }
}
