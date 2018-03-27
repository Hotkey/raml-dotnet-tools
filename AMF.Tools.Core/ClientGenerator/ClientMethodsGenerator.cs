﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using AMF.Tools.Core.WebApiGenerator;
using AMF.Parser.Model;
using Raml.Common;

namespace AMF.Tools.Core.ClientGenerator
{
    public class ClientMethodsGenerator : MethodsGeneratorBase
    {
        private readonly IDictionary<string, ApiObject> uriParameterObjects;
        private readonly IDictionary<string, ApiObject> queryObjects;
        private readonly IDictionary<string, ApiObject> headerObjects;
        private readonly IDictionary<string, ApiObject> responseHeadersObjects;
        private readonly string defaultHeaderType = typeof(HttpResponseHeaders).Name;
        private readonly QueryParametersParser queryParametersParser;

        public ClientMethodsGenerator(AmfModel raml, IDictionary<string, ApiObject> schemaResponseObjects, 
            IDictionary<string, ApiObject> uriParameterObjects, IDictionary<string, ApiObject> queryObjects, 
            IDictionary<string, ApiObject> headerObjects, IDictionary<string, ApiObject> responseHeadersObjects,
            IDictionary<string, ApiObject> schemaRequestObjects, IDictionary<string, string> linkKeysWithObjectNames,
            IDictionary<string, ApiObject> schemaObjects )
            : base(raml, schemaResponseObjects, schemaRequestObjects, linkKeysWithObjectNames, schemaObjects)
        {
            this.uriParameterObjects = uriParameterObjects;
            this.queryObjects = queryObjects;
            this.headerObjects = headerObjects;
            this.responseHeadersObjects = responseHeadersObjects;
            queryParametersParser = new QueryParametersParser(schemaObjects);
        }

        public ICollection<ClientGeneratorMethod> GetMethods(AMF.Parser.Model.EndPoint resource, string url, ClassObject parent, string objectName, IDictionary<string, Parameter> parentUriParameters)
        {
            var methodsNames = new List<string>();
            if (parent != null)
                methodsNames = parent.Methods.Select(m => m.Name).ToList();

            var generatorMethods = new Collection<ClientGeneratorMethod>();
            if (resource.Operations == null)
                return generatorMethods;

            foreach (var method in resource.Operations)
            {
                AddGeneratedMethod(resource, url, objectName, method, methodsNames, generatorMethods, parentUriParameters);
            }

            return generatorMethods;
        }

        private void AddGeneratedMethod(AMF.Parser.Model.EndPoint resource, string url, string objectName, Operation method, ICollection<string> methodsNames, 
            ICollection<ClientGeneratorMethod> generatorMethods, IDictionary<string, Parameter> parentUriParameters)
        {
            var generatedMethod = BuildClassMethod(url, method, resource);
            if (generatedMethod.ReturnType != "string")
            {
                var returnType = CollectionTypeHelper.GetBaseType(generatedMethod.ReturnType);

                var returnTypeObject = schemaObjects.Values.Any(o => o.Name == returnType)
                    ? schemaObjects.Values.First(o => o.Name == returnType)
                    : schemaResponseObjects.Values.FirstOrDefault(o => o.Name == returnType);

                if (returnTypeObject != null)
                {
                    generatedMethod.ReturnTypeObject = returnTypeObject;
                    generatedMethod.OkReturnType = GetOkReturnType(generatedMethod);
                }

            }
            uriParametersGenerator.Generate(resource, url, generatedMethod, uriParameterObjects, parentUriParameters);

            if (!IsVerbForMethod(method)) return;

            if (methodsNames.Contains(generatedMethod.Name))
                generatedMethod.Name = GetUniqueName(methodsNames, generatedMethod.Name, resource.Path);

            GetQueryParameters(objectName, method, generatedMethod);

            GetHeaders(objectName, method, generatedMethod);

            GetResponseHeaders(objectName, generatedMethod, method);

            generatorMethods.Add(generatedMethod);
            methodsNames.Add(generatedMethod.Name);
        }

        private string GetResourceType(IDictionary<string, IDictionary<string, string>> type)
        {
            return type != null && type.Any() ? type.First().Key : string.Empty;
        }

        private ClientGeneratorMethod BuildClassMethod(string url, Operation operation, AMF.Parser.Model.EndPoint resource)
        {
            var parentUrl = UrlGeneratorHelper.GetParentUri(url, resource.Path);

            //TODO: check
            var responseContentTypes = operation.Responses != null ?
                    operation.Responses.Where(r => r.Payloads != null).SelectMany(r => r.Payloads).Select(p => p.MediaType)
                    : new string[0];

            var generatedMethod = new ClientGeneratorMethod
            {
                Name = NetNamingMapper.GetMethodName(operation.Method ?? "Get" + resource.Path),
                ReturnType = GetReturnType(GeneratorServiceHelper.GetKeyForResource(operation, resource), operation, resource, url),
                Parameter = GetParameter(GeneratorServiceHelper.GetKeyForResource(operation, resource), operation, resource, url),
                Comment = GetComment(resource, operation, url),
                Url = url,
                Verb = NetNamingMapper.Capitalize(operation.Method),
                Parent = null,
                UseSecurity = resource.Operations.Any(m => m.Method == operation.Method && m.Security != null && m.Security.Any()),
                RequestContentTypes = operation.ContentType,
                ResponseContentTypes = responseContentTypes
            };

            // look in traits 
            
            // look in resource types

            return generatedMethod;
        }

        private static string GetOkReturnType(ClientGeneratorMethod generatedMethod)
        {
            if (!generatedMethod.ReturnTypeObject.IsMultiple)
                return generatedMethod.ReturnType;

            if (generatedMethod.ReturnTypeObject.Properties.Any(p => p.StatusCode == HttpStatusCode.OK))
                return generatedMethod.ReturnTypeObject.Properties.First(p => p.StatusCode == HttpStatusCode.OK).Type;

            return generatedMethod.ReturnTypeObject.Properties.First().Type;
        }

        private void GetQueryParameters(string objectName, Operation method, ClientGeneratorMethod generatedMethod)
        {
            if (method.Request.QueryParameters != null && method.Request.QueryParameters.Any())
            {
                var queryObject = queryParametersParser.GetQueryObject(generatedMethod, method, objectName);
                generatedMethod.Query = queryObject;
                if (!queryObjects.ContainsKey(queryObject.Name))
                    queryObjects.Add(queryObject.Name, queryObject);
            }
        }

        private void GetHeaders(string objectName, Operation method, ClientGeneratorMethod generatedMethod)
        {
            if (method.Request.Headers != null && method.Request.Headers.Any())
            {
                var headerObject = HeadersParser.GetHeadersObject(generatedMethod, method, objectName);
                generatedMethod.Header = headerObject;
                headerObjects.Add(headerObject.Name, headerObject);
            }
        }

        private void GetResponseHeaders(string objectName, ClientGeneratorMethod generatedMethod, Operation method)
        {
            generatedMethod.ResponseHeaders = new Dictionary<HttpStatusCode, ApiObject>();
            foreach (var resp in method.Responses.Where(r => r.Headers != null && r.Headers.Any()))
            {
                var headerObject = HeadersParser.GetHeadersObject(generatedMethod, resp, objectName);
                generatedMethod.ResponseHeaders.Add(ParserHelpers.GetHttpStatusCode(resp.StatusCode), headerObject);
                responseHeadersObjects.Add(headerObject.Name, headerObject);
            }

            if (!generatedMethod.ResponseHeaders.Any())
            {
                generatedMethod.ResponseHeaderType = defaultHeaderType;
            }
            else if (generatedMethod.ResponseHeaders.Count == 1)
            {
                generatedMethod.ResponseHeaderType = ClientGeneratorMethod.ModelsNamespacePrefix + generatedMethod.ResponseHeaders.First().Value.Name;
            }
            else
            {
                CreateMultipleType(generatedMethod);
            }
        }

        private void CreateMultipleType(ClientGeneratorMethod generatedMethod)
        {
            var properties = BuildProperties(generatedMethod);

            var name = NetNamingMapper.GetObjectName("Multiple" + generatedMethod.Name + "Header");

            var apiObject = new ApiObject
            {
                Name = name,
                Description = "Multiple Header Types " + string.Join(", ", properties.Select(p => p.Name)),
                Properties = properties,
                IsMultiple = true
            };
            responseHeadersObjects.Add(new KeyValuePair<string, ApiObject>(name, apiObject));

            generatedMethod.ResponseHeaderType = ClientGeneratorMethod.ModelsNamespacePrefix + name;
        }

        private static List<Property> BuildProperties(ClientGeneratorMethod generatedMethod)
        {
            var properties = generatedMethod.ResponseHeaders
                .Select(kv => new Property
                              {
                                  Name = kv.Value.Name,
                                  Description = kv.Value.Description,
                                  Example = kv.Value.Example,
                                  StatusCode = kv.Key,
                                  Type = kv.Value.Name
                              })
                .ToList();
            return properties;
        }
    }
}