/*using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace XmlMd
{
    public class Demangler
    {
        private Assembly Assembly;

        public Demangler(Assembly assembly)
        {
            Assembly = assembly;
        }

        private const BindingFlags BindAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private enum SpecialModifier
        {
            Array,
            ByRef,
            Pointer
        }

        public DocumentedType DemangleEntireType(MemberDocType type)
        {
            var asmType = DemangleTypeName(type.Type.Name);

            var methods = type.Methods.Select(m => DemangleMethodName(asmType, m)).Zip(type.Methods);
            var properties = type.Properties.Select(m => DemanglePropertyName(asmType, m)).Zip(type.Properties);
            var fields = type.Fields.Select(m => DemangleFieldName(asmType, m)).Zip(type.Fields);
            var events = type.Events.Select(m => DemangleEventName(asmType, m)).Zip(type.Events);

            return new DocumentedType(
                (asmType, type.Type),
                methods.ToList(),
                properties.ToList(),
                fields.ToList(),
                events.ToList()
            );
        }

        private static Type ApplyModifier(Type type, SpecialModifier modifier)
            => modifier switch
            {
                SpecialModifier.Array => type.MakeArrayType(),
                SpecialModifier.ByRef => type.MakeByRefType(),
                SpecialModifier.Pointer => type.MakePointerType(),
                _ => null!,
            };

        private static SpecialModifier[] GetSpecialModifiers(string type, out string stripped)
        {
            var mods = new List<SpecialModifier>();

            while (true)
            {
                if (type.EndsWith(SpecialEnding_ByRef))
                {
                    mods.Add(SpecialModifier.ByRef);
                    type = type[..^1];
                }
                else if (type.EndsWith(SpecialEnding_Pointer))
                {
                    mods.Add(SpecialModifier.Pointer);
                    type = type[..^1];
                }
                else if (type.EndsWith(SpecialEnding_Array))
                {
                    mods.Add(SpecialModifier.Array);
                    type = type[..^2];
                }
                else
                {
                    break;
                }
            }

            stripped = type;
            return mods.ToArray();
        }

        public Type DemangleTypeName(string doc, Type? context = null)
        {
            Type type;

            var mods = GetSpecialModifiers(doc, out doc);

            var kind = TypeParamKind(doc);

            if (kind == TypeParamRefKind.Type)
            {
                if (context is null)
                {
                    throw new ArgumentNullException("Type was generic but not context provided");
                }
                type = context.GetGenericArguments()[int.Parse(doc[1..])];
            }
            else if (kind == TypeParamRefKind.Method)
            {
                // ``0
                type = Type.MakeGenericMethodParameter(int.Parse(doc[2..]));
            }
            else
            {
                if (doc.Contains('{')) // generic
                {
                    var start = doc.IndexOf('{') + 1;
                    var end = doc.IndexOf('}');
                    var typeSig = doc[start..end];
                    var types = typeSig.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => DemangleTypeName(t)).ToArray();

                    doc = doc[..(start - 1)] + "`" + types.Length;
                    type = RecursiveGetTypeOrNested().MakeGenericType(types);
                }
                else
                {
                    type = RecursiveGetTypeOrNested();
                }
            }

            foreach (var modifier in mods)
            {
                type = ApplyModifier(type, modifier);
            }

            return type;

            Type RecursiveGetTypeOrNested()
            {
                var name = doc;
                // null if nested type
                var type = GetSingleType(Assembly, name);

                while (type is null)
                {
                    var dot = name.LastIndexOf('.');
                    name = name[..dot] + '+' + name[(dot + 1)..];
                    type = GetSingleType(Assembly, name);
                }

                return type;

                static Type? GetSingleType(Assembly assembly, string name)
                {
                    string s = assembly.FullName!;

                    Func<AssemblyName, Assembly> getAssembly = a =>
                    {
                        try
                        {
                            return Assembly.Load(a);
                        }
                        catch
                        {
                            return Assembly.LoadFrom(Path.Join(Path.GetDirectoryName(assembly.Location), a.Name + ".dll"));
                        }
                    };

                    Console.WriteLine(string.Join('\n', assembly.GetReferencedAssemblies()
                    .Select(getAssembly).Select(a => a.Location)));

                    return assembly.GetType(name, false) ?? assembly.GetReferencedAssemblies()
                        .Select(getAssembly).Select(a => a.GetType(name, false)).OfType<Type>().FirstOrDefault();
                }
            }
        }

        private enum TypeParamRefKind
        {
            None,
            Method,
            Type
        }

        private static TypeParamRefKind TypeParamKind(string arg)
            => arg.StartsWith("``") ? TypeParamRefKind.Method : (arg.StartsWith("`") ? TypeParamRefKind.Type : TypeParamRefKind.None);

        public MethodBase DemangleMethodName(Type type, DocumentationComment doc)
        {
            string noRet = doc.Name;
            Type? retType = null;
            var implicitOpIndex = noRet.IndexOf('~');
            if (implicitOpIndex != -1)
            {
                retType = DemangleTypeName(noRet[(implicitOpIndex + 1)..]);
                noRet = noRet[..implicitOpIndex];
            }

            var (name, numTypeParams, types) = GetTypesForInvokable(type, noRet);

            if (name == "#ctor")
            {
                var ctor = type.GetConstructor(BindAll, null, types, null)!;

                if (ctor is null)
                {
                    Debugger.Break();
                }

                return ctor!;
            }

            MethodInfo? method;
            if (implicitOpIndex != -1)
            {
                method = type.GetMethods(BindAll)
                    .Where(m => m.ReturnType == retType && m.Name == name && m.GetGenericArguments().Length == numTypeParams).FirstOrDefault()!;
            }
            else
            {
                method = type.GetMethod(
                    name,
                    numTypeParams,
                    BindAll,
                    null,
                    types,
                    null
                );
            }

            if (method is null)
            {
                // https://github.com/dotnet/roslyn/issues/46674
                // Function pointers don't work in XML docs. So we try and resolve this by getting a best-case scenario
                Console.WriteLine($"Method {name} wasn't found. Trying to resolve best candidate....");
                return type.GetMethods(BindAll)
                    .Where(m => m.Name == name && m.GetGenericArguments().Length == numTypeParams).FirstOrDefault()!;
            }

            return method!;
        }

        private (string Name, int NumTypeParams, Type[] Types) GetTypesForInvokable(Type type, string invokable)
        {
            var paren = invokable.IndexOf('(');
            var fullName = paren == -1 ? invokable : invokable[0..paren];
            var name = fullName[(fullName.LastIndexOf('.') + 1)..];

            var split = name.Split("``");
            var numTypeParams = split.Length > 1 ? int.Parse(split[1]) : 0;

            var sig = paren == -1 ? string.Empty : invokable[(paren + 1)..^1];

            var reps = new Queue<string>();
            while ((paren = sig.IndexOf('{')) != -1)
            {
                var end = sig.IndexOf('}') + 1;
                var gen = sig[paren..end];
                reps.Enqueue(gen);
                sig = sig = sig[..paren] + "<>" + (end >= sig.Length ? string.Empty : sig[end..]);
            }

            var args = sig.Split(',', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains("<>"))
                {
                    args[i] = args[i].Replace("<>", reps.Dequeue());
                }
            }

            var types = args.Select(arg => DemangleTypeName(arg, type)).ToArray();

            return (split[0], numTypeParams, types);
        }

        private const string SpecialEnding_ByRef = "@";
        private const string SpecialEnding_Pointer = "*";
        private const string SpecialEnding_Array = "[]";

        private string GetLastSegment(string s) => s[(s.LastIndexOf('.') + 1)..];

        public FieldInfo DemangleFieldName(Type type, DocumentationComment doc)
        {
            var field = GetLastSegment(doc.Name);
            return type.GetField(field, BindAll) ?? throw new MissingFieldException(field);
        }

        public PropertyInfo DemanglePropertyName(Type type, DocumentationComment doc)
        {
            if (doc.Name.IndexOf('(') != -1 /* indexer )
            {
                var (name, numTypeParams, types) = GetTypesForInvokable(type, doc.Name);
                return type.GetProperty(name, BindAll, null, null, types, null) ?? throw new MissingMemberException(name);
            }

            var prop = GetLastSegment(doc.Name);
            return type.GetProperty(prop, BindAll) ?? throw new MissingMemberException(prop);
        }

        public EventInfo DemangleEventName(Type type, DocumentationComment doc)
        {
            var @event = GetLastSegment(doc.Name);
            return type.GetEvent(@event, BindAll) ?? throw new MissingMemberException(@event);
        }
    }
}*/