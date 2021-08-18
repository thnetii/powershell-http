using System;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerShell.Commands;

namespace THNETII.PowerShellHttp
{
    public class PowerShellWebRequestHttpHandler : HttpMessageHandler
    {
        public X509Certificate? Certificate { get; set; }
        public string? CertificateThumbprint { get; set; }
        public PSCredential? Credential { get; set; }
        public bool? DisableKeepAlive { get; set; }
        public int? MaximumRedirection { get; set; }
        public Uri? Proxy { get; set; }
        public PSCredential? ProxyCredential { get; set; }
        public bool? ProxyUseDefaultCredentials { get; set; }
        public int? TimeoutSec { get; set; }
        public string? TransferEncoding { get; set; }
        public bool? UseBasicParsing { get; set; }
        public bool? UseDefaultCredentials { get; set; }
        public string? UserAgent { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var invokeWebReqCmd = new InvokeWebRequestCommand
            {
                Uri = request.RequestUri,
                Certificate = Certificate,
                CertificateThumbprint = CertificateThumbprint,
                Credential = Credential,
                Proxy = Proxy,
                ProxyCredential = ProxyCredential,
                TransferEncoding = TransferEncoding,
                UserAgent = UserAgent
            };
            if (DisableKeepAlive.HasValue)
                invokeWebReqCmd.DisableKeepAlive = DisableKeepAlive.GetValueOrDefault();
            if (MaximumRedirection.HasValue)
                invokeWebReqCmd.MaximumRedirection = MaximumRedirection.GetValueOrDefault();
            if (ProxyUseDefaultCredentials.HasValue)
                invokeWebReqCmd.ProxyUseDefaultCredentials = ProxyUseDefaultCredentials.GetValueOrDefault();
            if (TimeoutSec.HasValue)
                invokeWebReqCmd.TimeoutSec = TimeoutSec.GetValueOrDefault();
            if (UseBasicParsing.HasValue)
                invokeWebReqCmd.UseBasicParsing = UseBasicParsing.GetValueOrDefault();
            if (UseDefaultCredentials.HasValue)
                invokeWebReqCmd.UseDefaultCredentials = UseDefaultCredentials.GetValueOrDefault();

            if (Enum.TryParse<WebRequestMethod>(request.Method.Method, ignoreCase: true, out var webRequestMethod))
                invokeWebReqCmd.Method = webRequestMethod;
            else
            {
#if NETFRAMEWORK
                throw new PSNotSupportedException($"Unsupported non-standard HTTP method: {request.Method}");
#else // NETCOREAPP
                invokeWebReqCmd.CustomMethod = request.Method.Method;
#endif
            }
            invokeWebReqCmd.Headers = request.Headers?.ToDictionary(
                kvp => kvp.Key, kvp => (object)(kvp.Value.ToArray() switch
                {
                    { Length: 1 } singleElemArry => singleElemArry[0],
                    string[] array => array,
                    _ => "",
                }),
                StringComparer.OrdinalIgnoreCase);
            if (request.Content is not null)
            {
                return AddContentAndInvokeAsync(request, invokeWebReqCmd);
            }
            else
            {
                return Task.FromResult(InvokeCommand(request, invokeWebReqCmd));
            }
        }

        private async Task<HttpResponseMessage> AddContentAndInvokeAsync(
            HttpRequestMessage request,
            InvokeWebRequestCommand command)
        {
            byte[] content = await request.Content.ReadAsByteArrayAsync()
                .ConfigureAwait(continueOnCapturedContext: true);

            command.ContentType = request.Content.Headers.ContentType?.ToString();
            command.Body = content;
            foreach (var header in request.Content.Headers)
            {
                command.Headers[header.Key] = header.Value.ToArray() switch
                {
                    { Length: 1 } singleElemArry => singleElemArry[0],
                    string[] array => array,
                    _ => "",
                };
            }

            return InvokeCommand(request, command);
        }

        private static HttpResponseMessage InvokeCommand(
            HttpRequestMessage request,
            InvokeWebRequestCommand command)
        {
            var webResponse = command.Invoke<WebResponseObject>().Single();
#if NETFRAMEWORK
            var responseMessage = new HttpResponseMessage((HttpStatusCode)webResponse.StatusCode)
            {
                ReasonPhrase = webResponse.StatusDescription,
                RequestMessage = request,
                Content = new StreamContent(webResponse.RawContentStream)
            };
            foreach (var headerPair in webResponse.Headers)
            {
                responseMessage.Headers.Add(headerPair.Key, headerPair.Value);
            }
            return responseMessage;
#else // NETCOREAPP
            return webResponse.BaseResponse;
#endif
        }
    }
}
