using System.Globalization;
using System.IO;
using System.Reflection;

namespace MefContrib.Web.Mvc
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.ComponentModel.Composition.Hosting;
    using System.ComponentModel.Composition.Primitives;
    using System.Linq;
    using System.Web.Mvc;
    using MefContrib.Hosting.Filter;
    using MefContrib.Web.Mvc.Filter;

    /// <summary>
    /// CompositionDependencyResolver
    /// </summary>
    public class CompositionDependencyResolver
        : IDependencyResolver, IDependencyBuilder, IServiceProvider, ICompositionContainerProvider
    {
        /// <summary>
        /// HttpContext key for the container.
        /// </summary>
        public const string HttpContextKey = "__CompositionDependencyResolver_Container";

        private ComposablePartCatalog completeCatalog;
        private ComposablePartCatalog globalCatalog;
        private CompositionContainer globalContainer;
        private ComposablePartCatalog filteredCatalog;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositionDependencyResolver"/> class.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        public CompositionDependencyResolver(ComposablePartCatalog catalog)
        {
            // Keep the original catalog
            this.completeCatalog = catalog;

            // Filter the global part catalog to a set of parts that define PartCreationScope.Global.
            this.globalCatalog = new FilteringCatalog(
                this.completeCatalog, new HasPartCreationScope(PartCreationScope.Global));
            this.globalContainer = new CompositionContainer(this.globalCatalog, true, null);

            // Filter the per-request part catalog to a set of parts that define PartCreationScope.PerRequest.
            this.filteredCatalog = new FilteringCatalog(
                this.completeCatalog, new HasPartCreationScope(PartCreationScope.PerRequest));
        }

        /// <summary>
        /// Gets the global container.
        /// </summary>
        public CompositionContainer GlobalContainer
        {
            get
            {
                return this.globalContainer;
            }
        }

        /// <summary> 
        /// Gets the container.
        /// </summary>
        /// <value>The container.</value>
        public CompositionContainer Container
        {
            get
            {
                if (!CurrentRequestContext.Items.Contains(HttpContextKey))
                {
                    CurrentRequestContext.Items.Add(HttpContextKey,
                        new CompositionContainer(this.filteredCatalog, true, this.globalContainer));
                }

                return (CompositionContainer)CurrentRequestContext.Items[HttpContextKey];
            }
        }

        /// <summary>
        /// Handles composition exceptions and returns true if handled, false if the exception should be thrown. Can be overriden.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <returns>True if the exception has been handled, false if otherwise.</returns>
        public virtual bool HandleException(Exception exception)
        {
            var fileNotFoundException = exception as FileNotFoundException;
            if (fileNotFoundException != null)
            {
                var exceptionMessage = string.Format(CultureInfo.InvariantCulture,
                                                     "An error occurred while composing the MEF parts. Reason: {0}",
                                                     fileNotFoundException.FusionLog);
                throw new InvalidOperationException(exceptionMessage, fileNotFoundException);
            }

            var reflectionTypeLoadException = exception as ReflectionTypeLoadException;
            if (reflectionTypeLoadException != null)
            {
                var loaderExceptionMessages = reflectionTypeLoadException.LoaderExceptions.Select(loaderException => loaderException.Message).ToList();

                var loadedTypeNames = reflectionTypeLoadException.Types.Select(type => type != null ? type.FullName : null).ToList();

                var exceptionMessage = string.Format(CultureInfo.InvariantCulture,
                                                     "An error occurred while composing the MEF parts. Type(s): {0} , Reason(s): {1}",
                                                     string.Join(", ", loadedTypeNames),
                                                     string.Join(Environment.NewLine + "Next reason: ", loaderExceptionMessages));

                throw new InvalidOperationException(exceptionMessage, reflectionTypeLoadException);
            }

            return false;
        }

        /// <summary>
        /// Resolves singly registered services that support arbitrary object creation.
        /// </summary>
        /// <param name="serviceType">The type of the requested service or object.</param>
        /// <returns>The requested service or object.</returns>
        public object GetService(Type serviceType)
        {
            try
            {
                var exports = this.Container.GetExports(serviceType, null, null);
                if (exports.Any())
                {
                    return exports.First().Value;
                }
            }
            catch (Exception e)
            {
                if (!HandleException(e))
                {
                    throw;
                }
            }
            return null;
        }

        /// <summary>
        /// Resolves multiply registered services.
        /// </summary>
        /// <param name="serviceType">The type of the requested services.</param>
        /// <returns>The requested services.</returns>
        public IEnumerable<object> GetServices(Type serviceType)
        {
            try
            {
                var exports = this.Container.GetExports(serviceType, null, null);
                if (exports.Any())
                {
                    return exports.Select(e => e.Value).AsEnumerable();
                }
            }
            catch (Exception e)
            {
                if (!HandleException(e))
                {
                    throw;
                }
            }
            return new List<object>();
        }

        /// <summary>
        /// Builds the specified service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="service">The service.</param>
        /// <returns></returns>
        public T Build<T>(T service)
        {
            try
            {
                this.Container.SatisfyImportsOnce(service);
            }
            catch (Exception e)
            {
                if (!HandleException(e))
                {
                    throw;
                }
            }
            return service;
        }
    }
}
