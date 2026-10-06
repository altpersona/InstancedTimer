// apiscan - dump type/member signatures from a .NET assembly without a decompiler.
// Uses System.Reflection.Metadata (built into the .NET 6 runtime) to read
// assembly_valheim.dll read-only. Throwaway dev tool for the SunkenCryptTimer mod.
//
// Usage: dotnet run -- <assembly.dll> <NameSubstring> [NameSubstring...]
//        (case-insensitive substring match on full type name, nested types as Outer+Inner)

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var assemblyPath = args[0];
var filters = args.Skip(1).Select(a => a.ToLowerInvariant()).ToArray();
if (filters.Length == 0)
{
    Console.Error.WriteLine("need at least one type-name filter");
    return 2;
}

using var fs = File.OpenRead(assemblyPath);
using var pe = new PEReader(fs);
var reader = pe.GetMetadataReader();
var provider = new NameProvider();

// Precompute full names (with nesting chain) for every type definition.
var fullNames = new Dictionary<TypeDefinitionHandle, string>();
foreach (var handle in reader.TypeDefinitions)
{
    var td = reader.GetTypeDefinition(handle);
    var ns = reader.GetString(td.Namespace);
    var name = reader.GetString(td.Name);
    var declaring = td.GetDeclaringType();
    if (!declaring.IsNil)
    {
        var parent = ResolveFullName(declaring);
        fullNames[handle] = parent + "+" + name;
    }
    else
    {
        fullNames[handle] = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }
    continue;

    string ResolveFullName(TypeDefinitionHandle h)
    {
        if (fullNames.TryGetValue(h, out var s)) return s;
        var t = reader.GetTypeDefinition(h);
        var n = reader.GetString(t.Name);
        var d = t.GetDeclaringType();
        var full = d.IsNil
            ? (string.IsNullOrEmpty(reader.GetString(t.Namespace)) ? n : reader.GetString(t.Namespace) + "." + n)
            : ResolveFullName(d) + "+" + n;
        fullNames[h] = full;
        return full;
    }
}

foreach (var (handle, fullName) in fullNames)
{
    if (!filters.Any(f => fullName.ToLowerInvariant().Contains(f))) continue;
    var td = reader.GetTypeDefinition(handle);
    Console.WriteLine();
    Console.WriteLine($"=== {fullName} ===");

    Console.WriteLine("-- fields");
    foreach (var fh in td.GetFields())
    {
        var fd = reader.GetFieldDefinition(fh);
        var type = fd.DecodeSignature(provider, null);
        var attrs = "";
        if ((fd.Attributes & System.Reflection.FieldAttributes.Public) != 0) attrs = "public";
        else if ((fd.Attributes & System.Reflection.FieldAttributes.FamORAssem) != 0) attrs = "protected-internal";
        else if ((fd.Attributes & System.Reflection.FieldAttributes.Family) != 0) attrs = "protected";
        else if ((fd.Attributes & System.Reflection.FieldAttributes.Private) != 0) attrs = "private";
        var constVal = "";
        if ((fd.Attributes & System.Reflection.FieldAttributes.Literal) != 0)
        {
            var c = reader.GetConstant(fd.GetDefaultValue());
            var br = reader.GetBlobReader(c.Value);
            constVal = " = " + ConstantValue(br, c.TypeCode);
        }
        Console.WriteLine($"  {attrs} {reader.GetString(fd.Name)} : {type}{constVal}");
    }

    Console.WriteLine("-- methods");
    foreach (var mh in td.GetMethods())
    {
        var md = reader.GetMethodDefinition(mh);
        var mattrs = "";
        if ((md.Attributes & System.Reflection.MethodAttributes.Public) != 0) mattrs = "public";
        else if ((md.Attributes & System.Reflection.MethodAttributes.FamORAssem) != 0) mattrs = "protected-internal";
        else if ((md.Attributes & System.Reflection.MethodAttributes.Family) != 0) mattrs = "protected";
        else if ((md.Attributes & System.Reflection.MethodAttributes.Private) != 0) mattrs = "private";
        else if ((md.Attributes & System.Reflection.MethodAttributes.Assembly) != 0) mattrs = "internal";
        string sig;
        try
        {
            var ms = md.DecodeSignature(provider, null);
            sig = "(" + string.Join(", ", ms.ParameterTypes) + ") -> " + ms.ReturnType;
        }
        catch (Exception e)
        {
            sig = "<sig decode failed: " + e.Message + ">";
        }
        Console.WriteLine($"  {mattrs} {reader.GetString(md.Name)} : {sig}");
    }
}

return 0;

static string ConstantValue(BlobReader br, ConstantTypeCode code)
{
    try
    {
        return code switch
        {
            ConstantTypeCode.Int32 => br.ReadInt32().ToString(),
            ConstantTypeCode.Int64 => br.ReadInt64().ToString(),
            ConstantTypeCode.String => "\"" + br.ReadUTF16(br.Length) + "\"",
            _ => "(" + code + ")"
        };
    }
    catch { return "(unreadable)"; }
}

class NameProvider : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
    public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType + " pinned";
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var td = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(td.Namespace);
        var nm = reader.GetString(td.Name);
        return string.IsNullOrEmpty(ns) ? nm : ns + "." + nm;
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        var ns = reader.GetString(tr.Namespace);
        var nm = reader.GetString(tr.Name);
        return string.IsNullOrEmpty(ns) ? nm : ns + "." + nm;
    }
    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
}
