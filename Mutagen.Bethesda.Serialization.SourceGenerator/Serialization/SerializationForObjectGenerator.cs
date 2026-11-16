using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Mutagen.Bethesda.Serialization.SourceGenerator.Customizations;
using Mutagen.Bethesda.Serialization.SourceGenerator.Serialization.Fields;
using Mutagen.Bethesda.Serialization.SourceGenerator.Utility;
using Noggog;
using Noggog.StructuredStrings;
using Noggog.StructuredStrings.CSharp;

namespace Mutagen.Bethesda.Serialization.SourceGenerator.Serialization;

public class SerializationForObjectGenerator
{
    /// <summary>
    /// The generated code's reference to the public name a round-trip note is written under
    /// (Mutagen.Bethesda.Serialization.RoundTripNoteNames: each constant there is named as its value), so the names live
    /// in one place that readers of the output can use (C59, W14). The names below are also the suffixes of the
    /// Mutagen methods that give and take each note (RoundTripMixIn.Get/Set{name}).
    /// </summary>
    private static string NoteName(string name) => $"Mutagen.Bethesda.Serialization.RoundTripNoteNames.{name}";

    /// <summary>
    /// The field that carries a record's header FormID as stored, when Mutagen keeps one other than the FormID it writes
    /// for the record's FormKey (e.g. a Small plugin's own record stored with its full index). Written only then, so
    /// other output is unchanged (C205).
    /// </summary>
    private const string HeaderFormIdAsStoredName = "HeaderFormIdAsStored";

    /// <summary>
    /// Whether to write and read <see cref="HeaderFormIdAsStoredName"/>: on the major record base, when the Mutagen the
    /// code is generated against has RoundTripMixIn.GetHeaderFormIdAsStored and SetHeaderFormIdAsStored (C206). Against
    /// an older Mutagen nothing is generated for it.
    /// </summary>
    private static bool CarriesHeaderFormIdAsStored(CompilationUnit compilation, LoquiTypeSet obj)
        => HasRoundTripPair(compilation, obj, HeaderFormIdAsStoredName);

    /// <summary>
    /// The field that carries the FormIDs in a record's subrecords stored in another form than Mutagen writes (a
    /// Small/Medium plugin's own links with its full index, master indices past the list): RoundTripMixIn's opaque
    /// value, written only when there is one (C210)
    /// </summary>
    private const string OwnLinkFormsAsStoredName = "OwnLinkFormsAsStored";

    private static bool CarriesOwnLinkFormsAsStored(CompilationUnit compilation, LoquiTypeSet obj)
        => HasRoundTripPair(compilation, obj, OwnLinkFormsAsStoredName);

    /// <summary>
    /// The field that carries the labels of a record's child groups stored in another form than its header (a Medium
    /// plugin's full-index 02001DC3 for its record FD001DC3; RoundTripMixIn.GetGroupLabelFormsAsStored's opaque value),
    /// written only when there are some (C223; RoleplayersAlternateStart.esm: 336 labels)
    /// </summary>
    private const string GroupLabelFormsAsStoredName = "GroupLabelFormsAsStored";

    private static bool CarriesGroupLabelFormsAsStored(CompilationUnit compilation, LoquiTypeSet obj)
        => HasRoundTripPair(compilation, obj, GroupLabelFormsAsStoredName);

    /// <summary>
    /// The field that carries the order a record's subrecords had in its file, when it differs from Mutagen's
    /// (RoundTripMixIn.GetSubrecordOrder's opaque value), written only when there is one (C211)
    /// </summary>
    private const string SubrecordOrderName = "SubrecordOrder";

    private static bool CarriesSubrecordOrder(CompilationUnit compilation, LoquiTypeSet obj)
        => HasRoundTripPair(compilation, obj, SubrecordOrderName);

    /// <summary>
    /// The field that carries a compressed record's compressed bytes as read, base64, written only when Mutagen's own
    /// compression of the record wouldn't give those bytes again (FileLayoutMixIn.GetOriginalCompressedDataIfNotReproduced;
    /// C213): a record compressed with another encoder or level. A record the Creation Kit compressed is usually given
    /// back by Mutagen's compression and carries nothing.
    /// </summary>
    private const string OriginalCompressedDataName = "OriginalCompressedData";

    private static bool CarriesOriginalCompressedData(CompilationUnit compilation, LoquiTypeSet obj)
        => obj.Getter?.Name == "IMajorRecordGetter"
           && HasFileLayoutMembers(compilation, "GetOriginalCompressedDataIfNotReproduced", "SetOriginalCompressedData");

    /// <summary>
    /// The mod field that carries the empty top-level groups its file had (FileLayoutMixIn.GetEmptyTopLevelGroups'
    /// opaque value), written only when there are some (C215)
    /// </summary>
    private const string EmptyTopLevelGroupsName = "EmptyTopLevelGroups";

    private static bool CarriesEmptyTopLevelGroups(CompilationUnit compilation, bool isMod)
        => isMod && HasFileLayoutMembers(compilation, "GetEmptyTopLevelGroups", "SetEmptyTopLevelGroups");

    /// <summary>
    /// The cell field that carries the layout its children group was read with, when it differs from Mutagen's (which
    /// of the persistent and temporary groups came first, where the navmeshes sat; CellMixIn.GetChildGroupLayout's
    /// opaque value), written only when there is one (sfta01.esm: the cells that put the temporary group first)
    /// </summary>
    private const string ChildGroupLayoutName = "ChildGroupLayout";

    /// <summary>
    /// On a cell, the CellMixIn of its game when that has GetChildGroupLayout and SetChildGroupLayout (Starfield)
    /// </summary>
    private static string? CellChildGroupLayoutMixIn(CompilationUnit compilation, LoquiTypeSet obj)
    {
        if (obj.Getter?.Name != "ICellGetter" || obj.Getter.ContainingNamespace is not { } ns) return null;
        var name = ns.ToDisplayString() + ".CellMixIn";
        var mixIn = compilation.Compilation.GetTypeByMetadataName(name);
        return mixIn != null
               && mixIn.GetMembers("GetChildGroupLayout").Length > 0
               && mixIn.GetMembers("SetChildGroupLayout").Length > 0
            ? name
            : null;
    }

    /// <summary>
    /// The package note: the order of its data inputs' names and flags after the procedure tree as read
    /// (Package.ProcedureTreeDataOrder, C51), written only when it isn't by index (C59, W13)
    /// </summary>
    private const string ProcedureTreeDataOrderName = "ProcedureTreeDataOrder";

    /// <summary>
    /// On a package, its namespace when the Mutagen the code is generated against has PackageMixIn.GetProcedureTreeDataOrder
    /// and a public setter of Package.ProcedureTreeDataOrder (Skyrim, Fallout4, Starfield)
    /// </summary>
    private static string? PackageProcedureTreeDataOrderNamespace(CompilationUnit compilation, LoquiTypeSet obj)
    {
        if (obj.Getter?.Name != "IPackageGetter" || obj.Getter.ContainingNamespace is not { } ns) return null;
        var name = ns.ToDisplayString();
        var mixIn = compilation.Compilation.GetTypeByMetadataName(name + ".PackageMixIn");
        var package = compilation.Compilation.GetTypeByMetadataName(name + ".Package");
        return mixIn != null
               && mixIn.GetMembers("GetProcedureTreeDataOrder").Length > 0
               && package != null
               && package.GetMembers(ProcedureTreeDataOrderName).OfType<IPropertySymbol>()
                   .Any(p => p.SetMethod is { DeclaredAccessibility: Accessibility.Public })
            ? name
            : null;
    }

    /// <summary>
    /// Whether the Mutagen the code is generated against has all the given FileLayoutMixIn methods
    /// </summary>
    private static bool HasFileLayoutMembers(CompilationUnit compilation, params string[] names)
    {
        var mixIn = compilation.Compilation.GetTypeByMetadataName("Mutagen.Bethesda.Plugins.Records.FileLayoutMixIn");
        return mixIn != null && names.All(n => mixIn.GetMembers(n).Length > 0);
    }

    /// <summary>
    /// On a mod, whether the Mutagen the code is generated against keeps what a rebuilt mod needs to write back like its
    /// file: the own-FormID style, the header stats baseline and the overridden-forms baseline (RoundTripMixIn; C211).
    /// Each is written only when the mod has it.
    /// </summary>
    private static bool CarriesModRoundTripNotes(CompilationUnit compilation, bool isMod)
    {
        if (!isMod) return false;
        var mixIn = compilation.Compilation.GetTypeByMetadataName("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn");
        return mixIn != null
            && new[] { "OwnFormIdStyle", "HeaderStatsBaseline", "OverriddenFormsBaseline" }
                .All(n => mixIn.GetMembers("Get" + n).Length > 0 && mixIn.GetMembers("Set" + n).Length > 0);
    }

    /// <summary>
    /// On the major record base, whether the Mutagen the code is generated against has RoundTripMixIn.Get{name} and
    /// Set{name}
    /// </summary>
    private static bool HasRoundTripPair(CompilationUnit compilation, LoquiTypeSet obj, string name)
    {
        if (obj.Getter?.Name != "IMajorRecordGetter") return false;
        var mixIn = compilation.Compilation.GetTypeByMetadataName("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn");
        return mixIn != null
            && mixIn.GetMembers("Get" + name).Length > 0
            && mixIn.GetMembers("Set" + name).Length > 0;
    }

    private readonly LoquiNameRetriever _nameRetriever;
    private readonly SerializationFieldGenerator _forFieldGenerator;
    private readonly WhereClauseGenerator _whereClauseGenerator;
    private readonly LoquiSerializationNaming _loquiSerializationNaming;
    private readonly PropertyCollectionRetriever _propertyCollectionRetriever;
    private readonly CustomizationDriver _customizationDriver;
    private readonly ObjectTypeTester _modObjectTypeTester;
    private readonly ReleaseRetriever _releaseRetriever;
    private readonly ISerializationInterceptor[] _serializationInterceptors;
    private readonly HasFormVersionRetriever _hasFormVersionRetriever;
    private readonly Blacklist _blacklist;

    public SerializationForObjectGenerator(
        LoquiNameRetriever nameRetriever,
        SerializationFieldGenerator forFieldGenerator,
        WhereClauseGenerator whereClauseGenerator,
        LoquiSerializationNaming loquiSerializationNaming,
        PropertyCollectionRetriever propertyCollectionRetriever, 
        CustomizationDriver customizationDriver,
        ObjectTypeTester modObjectTypeTester,
        ReleaseRetriever releaseRetriever,
        ISerializationInterceptor[] serializationInterceptors, 
        HasFormVersionRetriever hasFormVersionRetriever,
        Blacklist blacklist)
    {
        _nameRetriever = nameRetriever;
        _forFieldGenerator = forFieldGenerator;
        _whereClauseGenerator = whereClauseGenerator;
        _loquiSerializationNaming = loquiSerializationNaming;
        _propertyCollectionRetriever = propertyCollectionRetriever;
        _customizationDriver = customizationDriver;
        _modObjectTypeTester = modObjectTypeTester;
        _releaseRetriever = releaseRetriever;
        _serializationInterceptors = serializationInterceptors;
        _hasFormVersionRetriever = hasFormVersionRetriever;
        _blacklist = blacklist;
    }
    
    public void Generate(
        CompilationUnit compilation, 
        LoquiTypeSet typeSet)
    {
        if (_blacklist.ShouldSkip(typeSet)) return;
        compilation.Context.CancellationToken.ThrowIfCancellationRequested();
        
        var baseType = compilation.Mapping.TryGetBaseClass(typeSet);
        var inheriting = compilation.Mapping.TryGetDeepInheritingClasses(typeSet);
        
        var sb = new StructuredStringBuilder();

        var properties = _propertyCollectionRetriever.GetPropertyCollection(
            compilation,
            typeSet);

        GenerateUsings(compilation, typeSet, sb, properties);

        GenerateWarningSuppressions(sb);

        using (sb.Namespace(typeSet.GetAny().ContainingNamespace.ToString()))
        {
        }
        
        if (!_loquiSerializationNaming.TryGetSerializationItems(typeSet.GetAny(), out var objSerializationItems)) return;
        
        var gens = GetGenerics(typeSet);

        var isConcrete = _modObjectTypeTester.IsConcrete(typeSet.Direct);

        var isGroup = _modObjectTypeTester.IsGroup(typeSet.Setter);
        
        using (var c = sb.Class(objSerializationItems.SerializationHousingClassName))
        {
            c.AccessModifier = AccessModifier.Internal;
            c.Static = true;
        }
        using (sb.CurlyBrace())
        {
            if (inheriting.Count > 0)
            {
                GenerateSerializeWithCheck(compilation, typeSet, sb, gens, inheriting);
            }
            
            GenerateSerialize(compilation, typeSet, sb, baseType, properties, gens);
            
            GenerateSerializeFields(compilation, typeSet, sb, baseType, properties, gens);

            GenerateFieldDefaults(compilation, typeSet, sb, baseType, properties, gens);
            
            if (inheriting.Count > 0)
            {
                GenerateHasSerializationItemsWithCheck(compilation, typeSet, sb, gens, inheriting);
            }
            
            GenerateHasSerializationItems(compilation, typeSet, sb, baseType, properties, gens);
            
            if (inheriting.Count > 0)
            {
                GenerateDeserializeWithCheck(compilation, typeSet, sb, gens, inheriting);
            }

            if (isConcrete && !isGroup)
            {
                GenerateDeserialize(compilation, typeSet, sb, properties, gens);
            }
            
            GenerateDeserializeInto(compilation, typeSet, sb, baseType, properties, gens);
        }
        sb.AppendLine();
        
        compilation.Context.AddSource(objSerializationItems.SerializationHousingFileName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private void GenerateWarningSuppressions(StructuredStringBuilder sb)
    {
        sb.AppendLine("#pragma warning disable CS1998 // No awaits used");
        sb.AppendLine("#pragma warning disable CS0618 // Obsolete");
        sb.AppendLine();
    }
    
    private string CancelAccessor(bool isMod) => isMod ? "cancel" : "metaData.Cancel";

    private void GenerateDeserialize(
        CompilationUnit compilation,
        LoquiTypeSet typeSet,
        StructuredStringBuilder sb,
        PropertyCollection properties,
        SerializationGenerics generics)
    {
        if (typeSet.Direct == null) return;
        
        var isMod = _modObjectTypeTester.IsModObject(typeSet.Getter);
        var isMajorRecord = _modObjectTypeTester.IsMajorRecordObject(typeSet.Getter);
        var isModHeader = _modObjectTypeTester.IsModHeader(typeSet.Getter);
        
        var genString = generics.ReaderGenericsString(isMod);
        using (var args = sb.Function($"public static async Task<{typeSet.Direct}> Deserialize{genString}"))
        {
            args.Add($"TReadObject reader");
            args.Add($"ISerializationReaderKernel<TReadObject> kernel");
            if (isMod)
            {
                args.Add($"ModKey modKey");
                if (_releaseRetriever.HasRelease(typeSet.GetAny()))
                {
                    args.Add($"{_releaseRetriever.GetReleaseName(typeSet.Getter!)}Release release");
                }

                args.Add($"IWorkDropoff? workDropoff");
                args.Add($"IFileSystem? fileSystem");
                args.Add($"ICreateStream? streamCreator");
                args.Add("TMeta? extraMeta");
                args.Add("ReadInto<ISerializationReaderKernel<TReadObject>, TReadObject, TMeta>? metaReader");
                args.Add("CancellationToken cancel");
            }
            else
            {
                args.Add("SerializationMetaData metaData");
            }
            args.Wheres.Add("where TReadObject : IContainStreamPackage");
            args.Wheres.AddRange(generics.ReaderWheres());
        }
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"{CancelAccessor(isMod)}.ThrowIfCancellationRequested();");
            if (isMajorRecord)
            {
                using (var f = sb.Call($"var obj = new {typeSet.Direct}"))
                {
                    f.Add("kernel.ExtractFormKey(reader)");
                    if (_releaseRetriever.HasRelease(typeSet.GetAny()))
                    {
                        f.Add($"metaData.Release.To{_releaseRetriever.GetReleaseName(typeSet.Getter!)}Release()");
                    }
                }
            }
            else if (isMod)
            {
                using (var f = sb.Call($"var obj = new {typeSet.Direct}"))
                {
                    f.Add("modKey");
                    if (_releaseRetriever.HasRelease(typeSet.GetAny()))
                    {
                        f.Add("release");
                    }
                }
            }
            else
            {
                sb.AppendLine($"var obj = new {typeSet.Direct}();");
            }

            if (isModHeader && _hasFormVersionRetriever.HasFormVersion(typeSet.GetAny()))
            {
                sb.AppendLine($"obj.FormVersion = metaData.Constants.DefaultFormVersion!.Value;");
            }
            using (var c = sb.Call($"await DeserializeInto{genString}"))
            {
                c.AddPassArg("reader");
                c.AddPassArg("kernel");
                c.AddPassArg("obj");
                if (isMod)
                {
                    c.AddPassArg("workDropoff");
                    c.AddPassArg("fileSystem");
                    c.AddPassArg("streamCreator");
                    c.AddPassArg("extraMeta");
                    c.AddPassArg("metaReader");
                    c.AddPassArg("cancel");
                }
                else
                {
                    c.AddPassArg("metaData");
                }
            }
            sb.AppendLine("return obj;");
        }
        sb.AppendLine();
    }

    private void GenerateDeserializeInto(
        CompilationUnit compilation,
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties,
        SerializationGenerics generics)
    {
        var isMod = _modObjectTypeTester.IsModObject(obj.Getter);
        var genString = generics.ReaderGenericsString(isMod);
        
        var hadIntoFields = GenerateDeserializeSingleFieldInto(compilation, obj, sb, baseType, properties, generics, genString);

        using (var args = sb.Function($"public static async Task DeserializeInto{genString}"))
        {
            args.Add($"TReadObject reader");
            args.Add($"ISerializationReaderKernel<TReadObject> kernel");
            args.Add($"{obj.DeserializeSymbol} obj");
            if (isMod)
            {
                args.Add("IWorkDropoff? workDropoff");
                args.Add("IFileSystem? fileSystem");
                args.Add($"ICreateStream? streamCreator");
                args.Add("TMeta? extraMeta");
                args.Add("ReadInto<ISerializationReaderKernel<TReadObject>, TReadObject, TMeta>? metaReader");
                args.Add("CancellationToken cancel");
            }
            else
            {
                args.Add("SerializationMetaData metaData");
            }
            args.Wheres.Add("where TReadObject : IContainStreamPackage");
            args.Wheres.AddRange(generics.ReaderWheres());
        }
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"{CancelAccessor(isMod)}.ThrowIfCancellationRequested();");
            if (isMod)
            {
                GenerateMetaConstruction(sb, "obj", "workDropoff", "fileSystem", "streamCreator", CancelAccessor(isMod));
            }

            _customizationDriver.SerializationPreWork(obj, compilation, sb, properties);

            GenerateDeserializeCall(compilation, obj, sb, baseType, properties, generics, 
                generateLoop: hadIntoFields);
            
            _customizationDriver.SerializationPostWork(obj, compilation, sb, properties);
        }
        sb.AppendLine();
    }

    private bool GenerateDeserializeSingleFieldInto(
        CompilationUnit compilation, 
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties, 
        SerializationGenerics generics,
        string genString)
    {        
        var hasInheritingClasses = compilation.Mapping.HasInheritingClasses(obj);
        var hasBaseClass = compilation.Mapping.TryGetBaseClass(obj) != null;

        var alwaysGenerate = hasInheritingClasses || hasBaseClass;
        var isMod = _modObjectTypeTester.IsModObject(obj.Getter);

        var orig = sb;
        sb = new StructuredStringBuilder();
        using (var args = sb.Function($"public static async Task DeserializeSingleFieldInto{genString}"))
        {
            args.Add($"TReadObject reader");
            args.Add($"ISerializationReaderKernel<TReadObject> kernel");
            args.Add($"{obj.DeserializeSymbol} obj");
            args.Add("SerializationMetaData metaData");
            args.Add("string name");

            if (isMod)
            {
                args.Add("TMeta? extraMeta");
                args.Add("ReadInto<ISerializationReaderKernel<TReadObject>, TReadObject, TMeta>? metaReader");
            }

            args.Wheres.Add("where TReadObject : IContainStreamPackage");
            args.Wheres.AddRange(generics.ReaderWheres());
        }
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"metaData.Cancel.ThrowIfCancellationRequested();");
            sb.AppendLine("switch (name)");
            using (sb.CurlyBrace())
            {
                bool generatedForProperty = false;
                foreach (var prop in properties.InOrder)
                {
                    compilation.Context.CancellationToken.ThrowIfCancellationRequested();
                    _customizationDriver.WrapOmission(compilation, sb, obj, prop, () =>
                    {
                        var subSb = new StructuredStringBuilder();
                        _forFieldGenerator.GenerateDeserializeForField(
                            compilation: compilation,
                            obj: obj,
                            fieldType: prop.Property.Type,
                            readerAccessor: "reader",
                            fieldName: prop.Property.Name,
                            fieldAccessor: $"obj.{prop.Property.Name}",
                            canSet: prop.Property.IsSettable(),
                            prop.Generator,
                            sb: subSb,
                            cancel: compilation.Context.CancellationToken);
                        if (subSb.Count == 0) return;
                        generatedForProperty = true;

                        sb.AppendLine($"case \"{prop.Property.Name}\":");
                        using (sb.IncreaseDepth())
                        {
                            sb.AppendLines(subSb);
                            sb.AppendLine("break;");
                        }
                    });
                }

                if (CarriesHeaderFormIdAsStored(compilation, obj))
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(HeaderFormIdAsStoredName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("if (kernel.ReadString(reader) is { } headerFormIdAsStored)");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetHeaderFormIdAsStored(obj, uint.Parse(headerFormIdAsStored, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture));");
                        }
                        sb.AppendLine("break;");
                    }
                }

                if (CarriesOwnLinkFormsAsStored(compilation, obj))
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(OwnLinkFormsAsStoredName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetOwnLinkFormsAsStored(obj, kernel.ReadString(reader));");
                        sb.AppendLine("break;");
                    }
                }

                if (CarriesGroupLabelFormsAsStored(compilation, obj))
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(GroupLabelFormsAsStoredName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetGroupLabelFormsAsStored(obj, kernel.ReadString(reader));");
                        sb.AppendLine("break;");
                    }
                }

                if (CarriesSubrecordOrder(compilation, obj))
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(SubrecordOrderName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetSubrecordOrder(obj, kernel.ReadString(reader));");
                        sb.AppendLine("break;");
                    }
                }

                if (CarriesOriginalCompressedData(compilation, obj))
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(OriginalCompressedDataName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("if (kernel.ReadString(reader) is { } originalCompressedData)");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("Mutagen.Bethesda.Plugins.Records.FileLayoutMixIn.SetOriginalCompressedData(obj, System.Convert.FromBase64String(originalCompressedData));");
                        }
                        sb.AppendLine("break;");
                    }
                }

                if (CellChildGroupLayoutMixIn(compilation, obj) is { } cellMixInForRead)
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(ChildGroupLayoutName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine($"{cellMixInForRead}.SetChildGroupLayout(obj, kernel.ReadString(reader));");
                        sb.AppendLine("break;");
                    }
                }

                if (PackageProcedureTreeDataOrderNamespace(compilation, obj) is { } packageNamespaceForRead)
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(ProcedureTreeDataOrderName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("var procedureTreeDataOrder = Mutagen.Bethesda.Serialization.Utility.RoundTripNotesHelper.ProcedureTreeDataOrderFromString(kernel.ReadString(reader));");
                        sb.AppendLine($"if (obj is {packageNamespaceForRead}.Package procedureTreeDataOrderPackage)");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("procedureTreeDataOrderPackage.ProcedureTreeDataOrder = procedureTreeDataOrder;");
                        }
                        sb.AppendLine("break;");
                    }
                }

                if (CarriesEmptyTopLevelGroups(compilation, isMod))
                {
                    generatedForProperty = true;
                    sb.AppendLine($"case {NoteName(EmptyTopLevelGroupsName)}:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("Mutagen.Bethesda.Plugins.Records.FileLayoutMixIn.SetEmptyTopLevelGroups(obj, kernel.ReadString(reader));");
                        sb.AppendLine("break;");
                    }
                }

                if (CarriesModRoundTripNotes(compilation, isMod))
                {
                    generatedForProperty = true;
                    sb.AppendLine("case " + NoteName("OwnFormIdStyle") + ":");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("if (kernel.ReadString(reader) is { } ownFormIdStyle)");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetOwnFormIdStyle(obj, Enum.Parse<Mutagen.Bethesda.Plugins.Records.ModOwnFormIdStyle>(ownFormIdStyle));");
                        }
                        sb.AppendLine("break;");
                    }
                    sb.AppendLine("case " + NoteName("HeaderStatsBaseline") + ":");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetHeaderStatsBaseline(obj, Mutagen.Bethesda.Serialization.Utility.RoundTripNotesHelper.HeaderStatsFromString(kernel.ReadString(reader)));");
                        sb.AppendLine("break;");
                    }
                    sb.AppendLine("case " + NoteName("OverriddenFormsBaseline") + ":");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine("Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.SetOverriddenFormsBaseline(obj, Mutagen.Bethesda.Serialization.Utility.RoundTripNotesHelper.FormKeysFromString(kernel.ReadString(reader)));");
                        sb.AppendLine("break;");
                    }
                }

                if (!generatedForProperty && !alwaysGenerate)
                {
                    return false;
                }

                sb.AppendLine("default:");
                using (sb.IncreaseDepth())
                {
                    if (baseType != null
                        && _loquiSerializationNaming.TryGetSerializationItems(baseType, out var baseSerializationItems))
                    {
                        sb.AppendLine(
                            $"await {baseSerializationItems.DeserializationSingleFieldIntoCall()}{genString}(reader, kernel, obj, metaData, name);");
                    }
                    else if (!isMod)
                    {
                        sb.AppendLine("kernel.Skip(reader);");
                    }
                    
                    if (isMod)
                    {
                        sb.AppendLine("if (extraMeta != null && metaReader != null && name.Equals(extraMeta.GetType().Name))");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("await metaReader(reader, extraMeta, kernel, metaData);");
                        }
                        sb.AppendLine("else");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("kernel.Skip(reader);");
                        }
                    }

                    sb.AppendLine("break;");
                }
            }
        }

        sb.AppendLine();
        orig.AppendLines(sb);
        return true;
    }

    private void GenerateDeserializeCall(
        CompilationUnit compilation, 
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties,
        SerializationGenerics generics,
        bool generateLoop)
    {
        foreach (var interceptor in _serializationInterceptors)
        {
            if (interceptor.Applicable(compilation, obj))
            {
                interceptor.GenerateDeserialize(
                    compilation,
                    obj,
                    sb,
                    baseType,
                    properties,
                    generics);
                return;
            }
        }
        
        var isMod = _modObjectTypeTester.IsModObject(obj.Getter);
        var genString = generics.ReaderGenericsString(isMod);

        if (generateLoop)
        {
            sb.AppendLine($"while (kernel.TryGetNextField(reader, out var name))");
            using (sb.CurlyBrace())
            {
                using (var c = sb.Call($"await DeserializeSingleFieldInto{genString}"))
                {
                    c.AddPassArg("reader");
                    c.AddPassArg("kernel");
                    c.AddPassArg("obj");
                    c.AddPassArg("metaData");
                    c.AddPassArg("name");
                    if (isMod)
                    {
                        c.AddPassArg("extraMeta");
                        c.AddPassArg("metaReader");
                    }
                }
            }            
            sb.AppendLine();
        }
        
        foreach (var prop in properties.InOrder)
        {
            compilation.Context.CancellationToken.ThrowIfCancellationRequested();
            _customizationDriver.WrapOmission(compilation, sb, obj, prop, () =>
            {
                _forFieldGenerator.GenerateForDeserializeSection(
                    compilation: compilation,
                    obj: obj,
                    fieldType: prop.Property.Type,
                    readerAccessor: "reader",
                    fieldName: prop.Property.Name,
                    fieldAccessor: $"obj.{prop.Property.Name}",
                    canSet: prop.Property.IsSettable(),
                    prop.Generator,
                    sb: sb,
                    cancel: compilation.Context.CancellationToken);
            });
        }
    }

    private void GenerateSerialize(
        CompilationUnit compilation,
        LoquiTypeSet typeSet,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties,
        SerializationGenerics generics)
    {
        foreach (var interceptor in _serializationInterceptors)
        {
            if (interceptor.Applicable(compilation, typeSet))
            {
                interceptor.GenerateSerialize(
                    compilation,
                    typeSet,
                    sb,
                    baseType,
                    properties,
                    generics);
                return;
            }
        }
        
        var genString = generics.WriterGenericsString(forHas: false);
        var isMod = _modObjectTypeTester.IsModObject(typeSet.Setter);
        using (var args = sb.Function($"public static async Task Serialize{genString}"))
        {
            args.Add($"TWriteObject writer");
            args.Add($"{typeSet.SerializeSymbol} item");
            args.Add($"MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel");
            if (isMod)
            {
                args.Add("IWorkDropoff? workDropoff");
                args.Add("IFileSystem? fileSystem");
                args.Add($"ICreateStream? streamCreator");
                args.Add("CancellationToken cancel");
            }
            else
            {
                args.Add("SerializationMetaData metaData");
            }
            args.Wheres.AddRange(generics.WriterWheres(forHas: false));
            args.Wheres.Add("where TWriteObject : IContainStreamPackage");
        }
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"{CancelAccessor(isMod)}.ThrowIfCancellationRequested();");
            if (isMod)
            {
                sb.AppendLine("var metaData = new SerializationMetaData(item.GameRelease, workDropoff, fileSystem, streamCreator, cancel);");
            }

            using (var args = sb.Call($"await SerializeFields{genString}"))
            {
                args.AddPassArg($"writer");
                args.AddPassArg($"item");
                args.AddPassArg($"kernel");
                args.AddPassArg($"metaData");
            }
        }
        sb.AppendLine();
    }

    private void GenerateSerializeFields(
        CompilationUnit compilation,
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties,
        SerializationGenerics generics)
    {
        var genString = generics.WriterGenericsString(forHas: false);
        using (var args = sb.Function($"public static async Task SerializeFields{genString}"))
        {
            args.Add($"TWriteObject writer");
            args.Add($"{obj.SerializeSymbol} item");
            args.Add($"MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel");
            args.Add("SerializationMetaData metaData");
            args.Wheres.AddRange(generics.WriterWheres(forHas: false));
            args.Wheres.Add("where TWriteObject : IContainStreamPackage");
        }
        
        var isMod = _modObjectTypeTester.IsModObject(obj.Setter);
        var isMajorRecord = _modObjectTypeTester.IsMajorRecordObject(obj.Setter);

        using (sb.CurlyBrace())
        {
            sb.AppendLine($"metaData.Cancel.ThrowIfCancellationRequested();");
            if (isMod)
            {
                sb.AppendLine($"kernel.WriteModKey(writer, \"ModKey\", item.ModKey, default, checkDefaults: false);");
                sb.AppendLine($"kernel.WriteEnum<GameRelease>(writer, \"GameRelease\", item.GameRelease, default, checkDefaults: false);");
            }

            StructuredStringBuilder sbInternal = new();
            if (baseType != null
                && _loquiSerializationNaming.TryGetSerializationItems(baseType, out var baseSerializationItems))
            {
                sbInternal.AppendLine(
                    $"await {baseSerializationItems.SerializationCall()}{genString}(writer, item, kernel, metaData);");
            }

            _customizationDriver.SerializationPreWork(obj, compilation, sbInternal, properties);
            
            foreach (var prop in properties.InOrder)
            {
                compilation.Context.CancellationToken.ThrowIfCancellationRequested();
                _customizationDriver.WrapOmission(compilation, sbInternal, obj, prop, () =>
                {
                    _forFieldGenerator.GenerateSerializeForField(
                        compilation: compilation,
                        obj: obj,
                        fieldType: prop.Property.Type,
                        writerAccessor: "writer",
                        fieldName: prop.Property.Name,
                        fieldAccessor: $"item.{prop.Property.Name}",
                        defaultValueAccessor: prop.DefaultString,
                        prop.Generator,
                        sb: sbInternal,
                        cancel: compilation.Context.CancellationToken,
                        alwaysWrite: prop.AlwaysWrite);
                });
            }

            // The round-trip notes, each written only when there is one, and not looked for at all when the writer
            // turned them off (SerializationMetaData.RoundTripNotes): finding some costs a write of the record (perf-world)
            if (CarriesHeaderFormIdAsStored(compilation, obj))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetHeaderFormIdAsStored(item) is { } headerFormIdAsStored)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(HeaderFormIdAsStoredName)}, headerFormIdAsStored.ToString(\"X8\"), default, checkDefaults: false);");
                }
            }

            if (CarriesOwnLinkFormsAsStored(compilation, obj))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetOwnLinkFormsAsStored(item) is { } ownLinkFormsAsStored)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(OwnLinkFormsAsStoredName)}, ownLinkFormsAsStored, default, checkDefaults: false);");
                }
            }

            if (CarriesGroupLabelFormsAsStored(compilation, obj))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetGroupLabelFormsAsStored(item) is { } groupLabelFormsAsStored)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(GroupLabelFormsAsStoredName)}, groupLabelFormsAsStored, default, checkDefaults: false);");
                }
            }

            if (CarriesSubrecordOrder(compilation, obj))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetSubrecordOrder(item) is { } subrecordOrder)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(SubrecordOrderName)}, subrecordOrder, default, checkDefaults: false);");
                }
            }

            if (CarriesOriginalCompressedData(compilation, obj))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.FileLayoutMixIn.GetOriginalCompressedDataIfNotReproduced(item) is { } originalCompressedData)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(OriginalCompressedDataName)}, System.Convert.ToBase64String(originalCompressedData), default, checkDefaults: false);");
                }
            }

            if (CellChildGroupLayoutMixIn(compilation, obj) is { } cellMixIn)
            {
                sbInternal.AppendLine($"if (metaData.RoundTripNotes && {cellMixIn}.GetChildGroupLayout(item) is {{ }} childGroupLayout)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(ChildGroupLayoutName)}, childGroupLayout, default, checkDefaults: false);");
                }
            }

            if (PackageProcedureTreeDataOrderNamespace(compilation, obj) is { } packageNamespace)
            {
                sbInternal.AppendLine($"if (metaData.RoundTripNotes && Mutagen.Bethesda.Serialization.Utility.RoundTripNotesHelper.ProcedureTreeDataOrderToString({packageNamespace}.PackageMixIn.GetProcedureTreeDataOrder(item)) is {{ }} procedureTreeDataOrder)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(ProcedureTreeDataOrderName)}, procedureTreeDataOrder, default, checkDefaults: false);");
                }
            }

            if (CarriesEmptyTopLevelGroups(compilation, isMod))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.FileLayoutMixIn.GetEmptyTopLevelGroups(item) is { } emptyTopLevelGroups)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine($"kernel.WriteString(writer, {NoteName(EmptyTopLevelGroupsName)}, emptyTopLevelGroups, default, checkDefaults: false);");
                }
            }

            if (CarriesModRoundTripNotes(compilation, isMod))
            {
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetOwnFormIdStyle(item) is var ownFormIdStyle && ownFormIdStyle != Mutagen.Bethesda.Plugins.Records.ModOwnFormIdStyle.Default)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine("kernel.WriteString(writer, " + NoteName("OwnFormIdStyle") + ", ownFormIdStyle.ToString(), default, checkDefaults: false);");
                }
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Serialization.Utility.RoundTripNotesHelper.HeaderStatsToString(Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetHeaderStatsBaseline(item)) is { } headerStatsBaseline)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine("kernel.WriteString(writer, " + NoteName("HeaderStatsBaseline") + ", headerStatsBaseline, default, checkDefaults: false);");
                }
                sbInternal.AppendLine("if (metaData.RoundTripNotes && Mutagen.Bethesda.Serialization.Utility.RoundTripNotesHelper.FormKeysToString(Mutagen.Bethesda.Plugins.Records.RoundTripMixIn.GetOverriddenFormsBaseline(item)) is { } overriddenFormsBaseline)");
                using (sbInternal.CurlyBrace())
                {
                    sbInternal.AppendLine("kernel.WriteString(writer, " + NoteName("OverriddenFormsBaseline") + ", overriddenFormsBaseline, default, checkDefaults: false);");
                }
            }

            _customizationDriver.SerializationPostWork(obj, compilation, sbInternal, properties);

            if (!sbInternal.Empty)
            {
                if (isMod || isMajorRecord)
                {
                    sb.AppendLine("try");
                    using (sb.CurlyBrace())
                    {
                        sb.AppendLines(sbInternal);
                    }

                    sb.AppendLine("catch (OperationCanceledException)");
                    using (sb.CurlyBrace())
                    {
                        sb.AppendLine("throw;");
                    }

                    if (compilation.Customization.Overall.SkipRecordsWithErrors)
                    {
                        sb.AppendLine("catch (Exception)");
                        using (sb.CurlyBrace())
                        {
                            // Swallow exception
                        }
                    }
                    else
                    {
                        sb.AppendLine("catch (Exception e)");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("SubrecordException.EnrichAndThrow(e, item);");
                            sb.AppendLine("throw;");
                        }
                    }
                }
                else
                {
                    sb.AppendLines(sbInternal);
                }
            }
        }
        sb.AppendLine();
    }

    /// <summary>
    /// FieldDefaults: the value each field whose written value is compared with a default is left out at (C59, S124),
    /// this class's and its base classes', by field name. Exactly the defaults the generated Serialize passes to the
    /// kernel; fields always written (FormVersion) and fields a customization omits are not listed. Not written for a
    /// class with generic arguments.
    /// </summary>
    private void GenerateFieldDefaults(
        CompilationUnit compilation,
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties,
        SerializationGenerics generics)
    {
        if (generics.ExtraWriterGenerics is { Count: > 0 }) return;
        string? baseCall = null;
        if (baseType != null
            && baseType is not INamedTypeSymbol { TypeArguments.Length: > 0 }
            && _loquiSerializationNaming.TryGetSerializationItems(baseType, out var baseItems))
        {
            baseCall = $"{baseItems.Namespace}.{baseItems.SerializationHousingClassName}.BuildFieldDefaults()";
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// The value each field is left out at when it holds it (unless the writer kernel writes defaults), by field name:");
        sb.AppendLine("/// the fields compared with a default, of this class and its base classes");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static IReadOnlyDictionary<string, object?> FieldDefaults => _fieldDefaults ??= BuildFieldDefaults();");
        sb.AppendLine("private static IReadOnlyDictionary<string, object?>? _fieldDefaults;");
        sb.AppendLine();
        sb.AppendLine("internal static Dictionary<string, object?> BuildFieldDefaults()");
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"var ret = {baseCall ?? "new Dictionary<string, object?>()"};");
            foreach (var prop in properties.InOrder)
            {
                compilation.Context.CancellationToken.ThrowIfCancellationRequested();
                if (prop.AlwaysWrite) continue;
                if (prop.Generator is not IDefaultComparingFieldGenerator comparing) continue;
                _customizationDriver.WrapOmission(compilation, sb, obj, prop, () =>
                {
                    sb.AppendLine($"ret[\"{prop.Property.Name}\"] = {comparing.ComparedDefault(prop.Property.Type, prop.DefaultString)};");
                });
            }
            sb.AppendLine("return ret;");
        }
        sb.AppendLine();
    }

    private void GenerateHasSerializationItems(
        CompilationUnit compilation,
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        ITypeSymbol? baseType,
        PropertyCollection properties,
        SerializationGenerics generics)
    {
        var isMod = _modObjectTypeTester.IsModObject(obj.Getter);
        var isMajorRecord = _modObjectTypeTester.IsMajorRecordObject(obj.Getter);
        using (var args = sb.Function($"public static bool HasSerializationItems{generics.WriterGenericsString(forHas: true)}"))
        {
            args.Add($"{obj.SerializeSymbol}? item");
            if (!isMod)
            {
                args.Add("SerializationMetaData metaData");
            }
            else
            {
                args.Add("CancellationToken cancel");
            }
            args.Wheres.AddRange(generics.WriterWheres(forHas: true));
        }

        using (sb.CurlyBrace())
        {
            sb.AppendLine($"{CancelAccessor(isMod)}.ThrowIfCancellationRequested();");
            sb.AppendLine("if (item == null) return false;");
            StructuredStringBuilder sbInternal = new();

            if (isMod)
            {
                GenerateMetaConstruction(sbInternal, "item", "null!", "null!", "null!", CancelAccessor(isMod));
            }
            if (baseType != null
                && _loquiSerializationNaming.TryGetSerializationItems(baseType, out var baseSerializationItems))
            {
                sbInternal.AppendLine(
                    $"if ({baseSerializationItems.HasSerializationCall()}{generics.WriterGenericsString(forHas: true)}(item, metaData)) return true;");
            }

            var hasInvariable = properties.InOrder.Any(x =>
            {
                var gen = _forFieldGenerator.GetGenerator(obj, compilation, x.Property.Type, x.Property.Name, isInsideCollection: false);
                return !gen?.HasVariableHasSerialize ?? true;
            });

            if (hasInvariable)
            {
                sbInternal.AppendLine("return true;");
            }
            else
            {
                foreach (var prop in properties.InOrder)
                {
                    compilation.Context.CancellationToken.ThrowIfCancellationRequested();
                    _customizationDriver.WrapOmission(compilation, sbInternal, obj, prop, () =>
                    {
                        _forFieldGenerator.GenerateHasSerializeForField(
                            compilation: compilation,
                            obj: obj, 
                            fieldType: prop.Property.Type,
                            fieldName: prop.Property.Name, 
                            fieldAccessor: $"item.{prop.Property.Name}", 
                            defaultValueAccessor: prop.DefaultString,
                            prop.Generator,
                            sb: sbInternal, 
                            cancel: compilation.Context.CancellationToken);
                    });
                }
            
                sbInternal.AppendLine("return false;");
            }
            
            if (!sbInternal.Empty)
            {
                if (isMod || isMajorRecord)
                {
                    sb.AppendLine("try");
                    using (sb.CurlyBrace())
                    {
                        sb.AppendLines(sbInternal);
                    }
                    sb.AppendLine("catch (OperationCanceledException)");
                    using (sb.CurlyBrace())
                    {
                        sb.AppendLine("throw;");
                    }

                    if (compilation.Customization.Overall.SkipRecordsWithErrors)
                    {
                        sb.AppendLine("catch (Exception)");
                        using (sb.CurlyBrace())
                        {
                            // Swallow exception
                        }
                    }
                    else
                    {
                        sb.AppendLine("catch (Exception e)");
                        using (sb.CurlyBrace())
                        {
                            sb.AppendLine("SubrecordException.EnrichAndThrow(e, item);");
                            sb.AppendLine("throw;");
                        }
                    }
                }
                else
                {
                    sb.AppendLines(sbInternal);
                }
            }
        }
        sb.AppendLine();
    }

    private void GenerateHasSerializationItemsWithCheck(
        CompilationUnit compilation,
        LoquiTypeSet typeSet,
        StructuredStringBuilder sb,
        SerializationGenerics generics,
        IReadOnlyCollection<LoquiTypeSet> inheriting)
    {
        var isMod = _modObjectTypeTester.IsModObject(typeSet.Setter);
        using (var args = sb.Function($"public static bool HasSerializationItemsWithCheck{generics.WriterGenericsString(forHas: true)}"))
        {
            args.Add($"{typeSet.Getter}? item");
            if (!isMod)
            {
                args.Add("SerializationMetaData metaData");
            }
            args.Wheres.AddRange(generics.WriterWheres(forHas: true));
        }

        using (sb.CurlyBrace())
        {
            // Need to encode inheriting type, so no short circuiting
            sb.AppendLine("return true;");
            
            // if (isMod)
            // {
            //     GenerateMetaConstruction(sb, "item");
            // }
            //
            // sb.AppendLine("switch (item)");
            // using (sb.CurlyBrace())
            // {
            //     foreach (var inherit in inheriting
            //                  .Select(x => x.Getter)
            //                  .OrderBy(x => x.Name))
            //     {
            //         context.CancellationToken.ThrowIfCancellationRequested();
            //         var names = _nameRetriever.GetNames(inherit);
            //         if (!_loquiSerializationNaming.TryGetSerializationItems(inherit, out var inheritSerializeItems))
            //             continue;
            //         if (!compilation.Mapping.TryGetTypeSet(inherit, out var inheritTypes)) continue;
            //         if (inheritTypes.Direct?.IsAbstract ?? true) continue;
            //         sb.AppendLine($"case {inherit.ContainingNamespace}.{names.Getter} {names.Direct}Getter:");
            //         using (sb.IncreaseDepth())
            //         {
            //             sb.AppendLine(
            //                 $"return {inheritSerializeItems.HasSerializationCall()}({names.Direct}Getter, metaData);");
            //         }
            //     }
            //
            //     if (_loquiSerializationNaming.TryGetSerializationItems(typeSet.Getter, out var curSerializationItems)
            //         && (!typeSet.Direct?.IsAbstract ?? false))
            //     {
            //         sb.AppendLine($"case {typeSet.Getter} {typeSet.Getter.Name}:");
            //         using (sb.IncreaseDepth())
            //         {
            //             sb.AppendLine(
            //                 $"return {curSerializationItems.HasSerializationCall()}({typeSet.Getter.Name}, metaData);");
            //         }
            //     }
            //
            //     sb.AppendLine("default:");
            //     using (sb.IncreaseDepth())
            //     {
            //         sb.AppendLine($"throw new NotImplementedException();");
            //     }
            // }
        }
        sb.AppendLine();
    }

    private void GenerateSerializeWithCheck(
        CompilationUnit compilation,
        LoquiTypeSet typeSet,
        StructuredStringBuilder sb, 
        SerializationGenerics generics,
        IReadOnlyCollection<LoquiTypeSet> inheriting)
    {
        if (typeSet.Getter == null) return;
        var isMod = _modObjectTypeTester.IsModObject(typeSet.Getter);
        var isMajorRecord = _modObjectTypeTester.IsMajorRecordObject(typeSet.Getter);
        var doCatch = isMod || isMajorRecord;
        using (var args = sb.Function($"public static async Task SerializeWithCheck{generics.WriterGenericsString(forHas: false)}"))
        {
            args.Add($"TWriteObject writer");
            args.Add($"{typeSet.Getter} item");
            args.Add($"MutagenSerializationWriterKernel<TKernel, TWriteObject> kernel");
            if (!isMod)
            {
                args.Add("SerializationMetaData metaData");
            }
            else
            {
                args.Add("CancellationToken cancel");
            }
            args.Wheres.AddRange(generics.WriterWheres(forHas: false));
            args.Wheres.Add("where TWriteObject : IContainStreamPackage");
        }
        using (sb.CurlyBrace())
        {
            if (doCatch)
            {
                sb.AppendLine("try");
            }
            using (sb.CurlyBrace(doIt: doCatch))
            {
                sb.AppendLine($"{CancelAccessor(isMod)}.ThrowIfCancellationRequested();");
                if (isMod)
                {
                    GenerateMetaConstruction(sb, "item", "null!", "null!", "null!", CancelAccessor(isMod));
                }

                sb.AppendLine($"kernel.WriteType(writer, item.GetType());");
                sb.AppendLine("switch (item)");
                using (sb.CurlyBrace())
                {
                    foreach (var inherit in inheriting
                                 .Select(x => x.Getter)
                                 .NotNull()
                                 .OrderBy(x => x.Name))
                    {
                        compilation.Context.CancellationToken.ThrowIfCancellationRequested();
                        var names = _nameRetriever.GetNames(inherit);
                        if (!_loquiSerializationNaming.TryGetSerializationItems(inherit, out var inheritSerializeItems))
                            continue;
                        if (!compilation.Mapping.TryGetTypeSet(inherit, out var inheritTypes)) continue;
                        if (inheritTypes.Direct?.IsAbstract ?? true) continue;

                        var genString = "";
                        if (inherit is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Length > 0)
                        {
                            genString = $"<{string.Join(", ", namedTypeSymbol.TypeArguments)}>";
                        }

                        sb.AppendLine(
                            $"case {inherit.ContainingNamespace}.{names.Getter}{genString} {names.Direct}Getter:");
                        using (sb.IncreaseDepth())
                        {
                            sb.AppendLine(
                                $"await {inheritSerializeItems.SerializationCall()}(writer, {names.Direct}Getter, kernel, metaData);");
                            sb.AppendLine("break;");
                        }
                    }

                    if (_loquiSerializationNaming.TryGetSerializationItems(typeSet.Getter,
                            out var curSerializationItems)
                        && (!typeSet.Direct?.IsAbstract ?? false))
                    {
                        sb.AppendLine($"case {typeSet.Getter} {typeSet.Getter.Name}:");
                        using (sb.IncreaseDepth())
                        {
                            sb.AppendLine(
                                $"await {curSerializationItems.SerializationCall()}(writer, {typeSet.Getter.Name}, kernel, metaData);");
                            sb.AppendLine("break;");
                        }
                    }

                    sb.AppendLine("default:");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine($"throw new NotImplementedException($\"Unknown object {{item}}\");");
                    }
                }
            }
            if (doCatch)
            {
                sb.AppendLine("catch (OperationCanceledException)");
                using (sb.CurlyBrace())
                {
                    sb.AppendLine("throw;");
                }

                if (compilation.Customization.Overall.SkipRecordsWithErrors)
                {
                    sb.AppendLine("catch (Exception)");
                    using (sb.CurlyBrace())
                    {
                        // Swallow exception
                    }
                }
                else
                {
                    sb.AppendLine("catch (Exception e)");
                    using (sb.CurlyBrace())
                    {
                        sb.AppendLine("SubrecordException.EnrichAndThrow(e, item);");
                        sb.AppendLine("throw;");
                    }
                }
            }
        }

        sb.AppendLine();
    }

    private void GenerateDeserializeWithCheck(
        CompilationUnit compilation,
        LoquiTypeSet typeSet,
        StructuredStringBuilder sb, 
        SerializationGenerics generics,
        IReadOnlyCollection<LoquiTypeSet> inheriting)
    {
        if (typeSet.Getter == null) return;
        var isMod = _modObjectTypeTester.IsModObject(typeSet.Getter);
        using (var args = sb.Function($"public static async Task<{typeSet.Direct ?? typeSet.Setter}> DeserializeWithCheck{generics.ReaderGenericsString(isMod)}"))
        {
            args.Add($"TReadObject reader");
            args.Add($"ISerializationReaderKernel<TReadObject> kernel");
            if (!isMod)
            {
                args.Add("SerializationMetaData metaData");
            }
            else
            {
                args.Add("CancellationToken cancel");
            }
            args.Wheres.Add("where TReadObject : IContainStreamPackage");
            args.Wheres.AddRange(generics.ReaderWheres());
        }
        using (sb.CurlyBrace())
        {
            sb.AppendLine($"{CancelAccessor(isMod)}.ThrowIfCancellationRequested();");
            sb.AppendLine($"var type = kernel.GetNextType(reader, \"{typeSet.GetAny().ContainingNamespace}\");");
            sb.AppendLine($"switch (type.GetSimpleName())");
            using (sb.CurlyBrace())
            {
                foreach (var inherit in inheriting
                             .Select(x => x.Getter)
                             .NotNull()
                             .OrderBy(x => x.Name))
                {
                    compilation.Context.CancellationToken.ThrowIfCancellationRequested();
                    var names = _nameRetriever.GetNames(inherit);
                    if (!_loquiSerializationNaming.TryGetSerializationItems(inherit, out var inheritSerializeItems))
                        continue;
                    if (!compilation.Mapping.TryGetTypeSet(inherit, out var inheritTypes)) continue;
                    if (inheritTypes.Direct?.IsAbstract ?? true) continue;
                    
                    var genString = "TReadObject";
                    if (inherit is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Length > 0)
                    {
                        genString += ", " + string.Join(", ", namedTypeSymbol.TypeArguments);
                    }
                    
                    sb.AppendLine($"case \"{names.Direct}\":");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine($"return await {inheritSerializeItems.DeserializationCall()}<{genString}>(reader, kernel, metaData);");
                    }
                }

                if (_loquiSerializationNaming.TryGetSerializationItems(typeSet.Getter, out var curSerializationItems)
                    && (!typeSet.Direct?.IsAbstract ?? false))
                {
                    sb.AppendLine($"case \"{typeSet.Direct?.Name}\":");
                    using (sb.IncreaseDepth())
                    {
                        sb.AppendLine($"return await {curSerializationItems.DeserializationCall()}(reader, kernel, metaData);");
                    }
                }

                sb.AppendLine("default:");
                using (sb.IncreaseDepth())
                {
                    sb.AppendLine($"throw new NotImplementedException($\"Unknown object name {{type.GetSimpleName()}}\");");
                }
            }
        }

        sb.AppendLine();
    }

    private static void GenerateUsings(
        CompilationUnit compilation,
        LoquiTypeSet obj,
        StructuredStringBuilder sb,
        PropertyCollection propertyDict)
    {
         sb.AppendLines(propertyDict.InOrder
            .SelectMany(x =>
                x.Generator?.RequiredNamespaces(obj, compilation, x.Property.Type) ?? Enumerable.Empty<string>())
            .And("Mutagen.Bethesda.Plugins")
            .And("Mutagen.Bethesda.Serialization")
            .And("Mutagen.Bethesda.Serialization.Streams")
            .And("Mutagen.Bethesda.Serialization.Utility")
            .And("Mutagen.Bethesda.Serialization.Exceptions")
            .And("System.Threading.Tasks")
            .And("System.IO.Abstractions")
            .And("Noggog.WorkEngine")
            .And("Loqui")
            .And("Noggog")
            .And("Noggog.IO")
            .And("Mutagen.Bethesda.Plugins.Exceptions")
            .And(obj.GetAny().ContainingNamespace.ToString())
            .Distinct()
            .OrderBy(x => x)
            .Select(x => $"using {x};"));
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }

    private SerializationGenerics GetGenerics(LoquiTypeSet typeSet)
    {
        SerializationGenerics? ret = null;
        
        if (typeSet.Getter is INamedTypeSymbol writerTypeSymbol
            && writerTypeSymbol.TypeArguments.Length > 0)
        {
            ret ??= new();
            ret.ExtraWriterGenerics = new();
            ret.ExtraWriterGenerics.AddRange(writerTypeSymbol.TypeArguments.Select(x => x.ToString()));
            ret.ExtraWriterWheres = new();
            ret.ExtraWriterWheres.AddRange(_whereClauseGenerator.GetWheres(writerTypeSymbol));
        }

        if (typeSet.Setter is INamedTypeSymbol readerTypeSymbol
            && readerTypeSymbol.TypeArguments.Length > 0)
        {
            ret ??= new();
            ret.ExtraReaderGenerics = new();
            ret.ExtraReaderGenerics.AddRange(readerTypeSymbol.TypeArguments.Select(x => x.ToString()));
            ret.ExtraReaderWheres = new();
            ret.ExtraReaderWheres.AddRange(_whereClauseGenerator.GetWheres(readerTypeSymbol));
        }

        return ret ?? SerializationGenerics.Instance;
    }

    private void GenerateMetaConstruction(
        StructuredStringBuilder sb, string accessor, string workDropoffAccessor,
        string fileSystemAccessor, string streamCreatorAccessor, string cancelAccessor)
    {
        sb.AppendLine($"var metaData = new SerializationMetaData({accessor}.GameRelease, {workDropoffAccessor}, {fileSystemAccessor}, {streamCreatorAccessor}, {cancelAccessor});");
    }
}