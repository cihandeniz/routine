﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Routine.Core;
using Routine.Core.Rest;
using Routine.Engine.Context;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;

namespace Routine.Service.RequestHandlers
{
    public abstract class RequestHandlerBase : IRequestHandler
    {
        #region Constants

        private const int CACHE_DURATION = 60;
        private const int BUFFER_SIZE = 0x1000;
        protected const string JSON_CONTENT_TYPE = "application/json";
        protected readonly Encoding DEFAULT_CONTENT_ENCODING = Encoding.UTF8;
        //private static ConcurrentDictionary<string, List<ObjectModel>> _modelIndex;
        #endregion

        #region Construction

        protected IServiceContext ServiceContext { get; }
        protected IJsonSerializer JsonSerializer { get; }
        protected IHttpContextAccessor HttpContextAccessor { get; }
        protected IMemoryCache MemoryCache { get; }

        protected RequestHandlerBase(IServiceContext serviceContext, IJsonSerializer jsonSerializer, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
        {
            ServiceContext = serviceContext;
            JsonSerializer = jsonSerializer;
            HttpContextAccessor = httpContextAccessor;
            MemoryCache = memoryCache;
        }

        #endregion

        public abstract void WriteResponse();

        protected HttpContext HttpContext => HttpContextAccessor.HttpContext;
        protected IHttpResponseFeature HttpResponseFeature => HttpContext.Response.HttpContext.Features.Get<IHttpResponseFeature>();
        protected RouteData RouteData => HttpContext.GetRouteData();
        protected IQueryCollection QueryString => HttpContext.Request.Query;
        protected string UrlBase => ServiceContext.ServiceConfiguration.GetPath(string.Empty).BeforeLast('/');
        protected bool IsGet => "GET".Equals(HttpContext.Request.Method, StringComparison.InvariantCultureIgnoreCase);
        protected bool IsPost => "POST".Equals(HttpContext.Request.Method, StringComparison.InvariantCultureIgnoreCase);
        protected ApplicationModel ApplicationModel => ServiceContext.ObjectService.ApplicationModel;

        static object _cacheLockObject = new();
        const string MODEL_INDEX_CACHE_KEY = "Routine.RequestHandler.ModelIndex";
        protected virtual Dictionary<string, List<ObjectModel>> ModelIndex
        {
            get
            {
                //todo: cache kurgusu olmali mi?
                HttpContext.Items.TryGetValue(MODEL_INDEX_CACHE_KEY, out var result);
                //var result = (Dictionary<string, List<ObjectModel>>)HttpContext.Items["Routine.RequestHandler.ModelIndex"];
                if (result != null) { return (Dictionary<string, List<ObjectModel>>)result; }

                //if (_modelIndex == default || _modelIndex.IsEmpty)
                //{
                //    _modelIndex = new ConcurrentDictionary<string, List<ObjectModel>>(BuildModelIndex());
                //    result = new Dictionary<string, List<ObjectModel>>(_modelIndex);
                //}

                lock (_cacheLockObject)
                {
                    result = MemoryCache.GetOrCreate(MODEL_INDEX_CACHE_KEY, cache =>
                       {
                           cache.AbsoluteExpiration = DateTimeOffset.MaxValue;

                           return BuildModelIndex();
                       });

                    HttpContext.Items.Add("Routine.RequestHandler.ModelIndex", result);
                }

                return (Dictionary<string, List<ObjectModel>>)result;

                // HttpContext.Application.Lock();

                // result = (Dictionary<string, List<ObjectModel>>)HttpContext.Application["Routine.RequestHandler.ModelIndex"]; ;

                // if (result != null)
                // {
                //     HttpContext.Application.UnLock();

                //     return result;
                // }

                //result = BuildModelIndex();
                //HttpContext.Items.Add("Routine.RequestHandler.ModelIndex", result);
                // HttpContext.Application["Routine.RequestHandler.ModelIndex"] = result;

                // HttpContext.Application.UnLock();


                //return result;
            }
        }

        protected virtual void AddResponseCaching()
        {
            var headers = HttpContext.Response.GetTypedHeaders();
            headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
            {
                Public = true,
                MaxAge = new TimeSpan(0, CACHE_DURATION, 0)
            };
            headers.Expires = DateTime.Now.AddMinutes(CACHE_DURATION);
        }

        protected virtual void BadRequest(Exception ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            HttpContext.Response.Headers["X-Status-Description"] = $"Cannot resolve parameters from request body. The exception is; {ex}";
        }

        protected virtual void ModelNotFound(TypeNotFoundException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            HttpContext.Response.Headers["X-Status-Description"] = $"Specified model ({ex.TypeId}) was not found in service model. The exception is; {ex}";
        }

        protected virtual void MethodNotAllowed(bool allowGet)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            HttpContext.Response.Headers["X-Status-Description"] = allowGet ? "Only GET, POST and OPTIONS are supported" : "Only POST and OPTIONS are supported";
        }

        protected virtual void WriteFileResponse(string path)
        {
            var stream = GetStream(path);

            var sr = new StreamReader(stream);

            var fileContent = sr.ReadToEnd();
            sr.Close();
            stream.Close();

            fileContent = fileContent.Replace("$urlbase$", UrlBase);

            AddResponseCaching();
            HttpContext.Response.ContentType = MimeTypeMap.GetMimeType(path.AfterLast("."));
            HttpContext.Response.Body.Write(Encoding.UTF8.GetBytes(fileContent));
        }

        protected virtual void WriteFontResponse(string fileName)
        {
            var stream = GetStream("assets/fonts/" + fileName);

            var outputStream = new MemoryStream();

            outputStream.Position = 0;

            using (stream)
            {
                var buffer = new byte[BUFFER_SIZE];

                while (true)
                {
                    var bytesRead = stream.Read(buffer, 0, BUFFER_SIZE);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    outputStream.Write(buffer, 0, bytesRead);
                }
            }

            AddResponseCaching();
            HttpContext.Response.ContentType = MimeTypeMap.GetMimeType(fileName);
            //todo: https://stackoverflow.com/questions/53194200/how-httpcontext-response-end-in-asp-net-core linkine gore statu code'u setlenmeli mi test edilecek
            // HttpContext.Response.Flush();
            // HttpContext.Response.End();

            Task.Run(async () =>
            {
                await HttpContext.Response.Body.WriteAsync(outputStream.ToArray(), new CancellationTokenSource().Token);
                HttpContext.Response.Body.Flush();
            }).Wait(CancellationToken.None);
        }

        protected virtual void WriteJsonResponse(object result, HttpStatusCode statusCode = HttpStatusCode.OK, bool clearError = false)
        {
            //todo:HttpContext.ClearError muadili bulunmali
            // if (clearError)
            // {
            //     HttpContext.ClearError();
            // }

            HttpContext.Response.StatusCode = (int)statusCode;

            var mediaType = new MediaTypeHeaderValue(JSON_CONTENT_TYPE);
            mediaType.Encoding = DEFAULT_CONTENT_ENCODING;
            HttpContext.Response.ContentType = mediaType.ToString();

            if (result != null)
            {
                HttpContext.Response.Body.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result)));
            }
        }

        private Dictionary<string, List<ObjectModel>> BuildModelIndex()
        {
            var result = new Dictionary<string, List<ObjectModel>>();
            var appModel = ServiceContext.ObjectService.ApplicationModel;

            foreach (var key in appModel.Model.Keys)
            {
                var shortModelId = key.AfterLast(".");
                if (!result.ContainsKey(shortModelId))
                {
                    result.Add(shortModelId, new List<ObjectModel>());
                }

                result[shortModelId].Add(appModel.Model[key]);
            }

            return result;
        }

        private Stream GetStream(string path)
        {
            path = path.Replace("/", ".");
            var stream = GetType().Assembly.GetManifestResourceStream(
                GetType().Assembly.GetManifestResourceNames().Single(s => s.EndsWith(path))
            );

            if (stream == null)
            {
                throw new InvalidOperationException("Could not get manifest resource stream for test page");
            }

            return stream;
        }
    }
}