using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;

namespace UmbracoArchiveProcessor.Template
{
    /// <remarks>
    /// https://github.com/Antaris/RazorEngine/issues/416#issuecomment-290428637
    /// </remarks>
    public class SystemRuntimeResolver : IReferenceResolver
    {
        private static readonly IReferenceResolver _resolver = new UseCurrentAssembliesReferenceResolver();
        private static readonly Assembly _systemRuntime = Assembly.Load("System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        public IEnumerable<CompilerReference> GetReferences(TypeContext context, IEnumerable<CompilerReference> includeAssemblies = null)
        {
            IEnumerable<CompilerReference> assemblies;

            var newReference = new[] { CompilerReference.From(_systemRuntime), };

            if (includeAssemblies == null)
            {
                assemblies = newReference;
            }
            else
            {
                assemblies = includeAssemblies.Concat(newReference);
            }

            return _resolver.GetReferences(context, assemblies);
        }
    }
}