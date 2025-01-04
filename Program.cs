using System.Net.Mail;
using System.Net.Mime;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowAllOrigins");

app.MapPost("/api/email", async (HttpContext context) =>
{
    try
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var form = context.Request.Form;
        var nome = form["Nome"];
        var email = form["Email"];
        string conteudo = form["Conteudo"].ToString();

       using var stream = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Content()
                    .Padding(50)
                    .Column(column =>
                    {
                        column.Item().Text("Contrato").FontSize(20).Bold();
                        column.Item().Text(conteudo).FontSize(12);
                    });
            });
        })
        .GeneratePdf(stream);

        stream.Position = 0; //Reseta a posição para o início so stream

        var smtpSettings = builder.Configuration.GetSection("Smtp").Get<SmtpSettings>();

        var smtpClient = new SmtpClient(smtpSettings.Host)
        {
            Port = smtpSettings.Port,
            Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password),
            EnableSsl = true,
        };


        var mailMessage = new MailMessage
        {
            From = new MailAddress("carlinhostvd@gmail.com"),
            Subject = $"Contrato de {nome}",
            Body = "Segue em anexo o contrato.",
            IsBodyHtml = false,
        };

        // Anexar o arquivo PDF
        //mailMessage.Attachments.Add(new Attachment(new MemoryStream(stream.ToArray()), "Contrato.pdf", MediaTypeNames.Application.Pdf));
        mailMessage.Attachments.Add(new Attachment(stream, "Contrato.pdf", MediaTypeNames.Application.Pdf));
        
        mailMessage.To.Add(email);

        await smtpClient.SendMailAsync(mailMessage);

        return Results.Ok("E-mail enviado com sucesso!");
    }
    catch (SmtpException smtpEx)
    {
        return Results.Problem($"Erro ao enviar e-mail: {smtpEx.StatusCode} - {smtpEx.Message}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro inesperado: {ex.Message}");
    }
});

app.Run();