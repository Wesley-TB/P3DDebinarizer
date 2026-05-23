using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace P3DDebin.Core;

// Extrai conteudo embutido de um P3D binarizado (ODOL):
// - RVMATs reconstruidos a partir de EmbeddedMaterial
// - model.cfg reconstruido a partir do esqueleto/animacoes (igual ao Eliteness)
public static class OdolExtract
{
    private const BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public sealed record TextItem(string Name, string Content);

    // Reconstroi os RVMATs embutidos em memoria (sem gravar em disco).
    // Name e um caminho relativo seguro; Content e o texto do .rvmat.
    //
    // Tratamento de obfuscacao do pboProject: quando o materialName traz bytes
    // de controle / nao-ASCII (tipico do flag -O do pboProject), o conteudo
    // do .rvmat continua intacto, mas o NOME nao serve para gravar. Nesse
    // caso o arquivo e renomeado para  obfuscated_material_NNN.rvmat .
    public static List<TextItem> GetRvmats(string inputPath, Action<string> log)
    {
        object odol = LoadOrThrow(inputPath, log);

        var lods = (Array?)odol.GetType().GetField("lods", MemberFlags)?.GetValue(odol)
                   ?? throw new InvalidOperationException("Campo 'lods' nao encontrado no ODOL.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<TextItem>();
        int obfuscatedCount = 0;

        foreach (object? lod in lods)
        {
            if (lod == null)
                continue;

            if (lod.GetType().GetField("materials", MemberFlags)?.GetValue(lod) is not Array materials)
                continue;

            foreach (object? mat in materials)
            {
                if (mat == null)
                    continue;

                string? name = mat.GetType().GetField("materialName", MemberFlags)?.GetValue(mat) as string;
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;

                var writeToFile = mat.GetType().GetMethod("writeToFile", new[] { typeof(string) })
                                  ?? throw new InvalidOperationException("EmbeddedMaterial.writeToFile nao encontrado.");

                string temp = Path.GetTempFileName();
                try
                {
                    Invoke(writeToFile, mat, temp);
                    string content = File.ReadAllText(temp);

                    string outputName;
                    if (IsObfuscatedPath(name))
                    {
                        obfuscatedCount++;
                        outputName = $"obfuscated_material_{obfuscatedCount:D3}.rvmat";
                        log(Strings.T("Core.LogRvmatObfuscated", EscapeForLog(name), outputName));
                    }
                    else
                    {
                        outputName = RelativeSafePath(name, ".rvmat");
                        log(Strings.T("Core.LogRvmat", name));
                    }

                    items.Add(new TextItem(outputName, content));
                }
                finally
                {
                    File.Delete(temp);
                }
            }
        }

        if (obfuscatedCount > 0)
            log(Strings.T("Core.LogRvmatsWarning", obfuscatedCount));

        return items;
    }

    // Heuristica: um caminho do Arma e sempre ASCII imprimivel. Qualquer byte
    // de controle ou alem de 0x7E indica obfuscacao do pboProject.
    private static bool IsObfuscatedPath(string s)
    {
        if (string.IsNullOrEmpty(s))
            return true;
        foreach (char c in s)
            if (c < 0x20 || c > 0x7E)
                return true;
        return false;
    }

    // Apresenta uma string com bytes nao-imprimiveis em \xNN para nao poluir o log.
    private static string EscapeForLog(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            if (c >= 0x20 && c <= 0x7E)
                sb.Append(c);
            else
                sb.Append($"\\x{(int)c:X2}");
        }
        return sb.ToString();
    }

    public static int ExtractRvmats(string inputPath, string outputFolder, Action<string> log)
    {
        List<TextItem> items = GetRvmats(inputPath, log);

        foreach (TextItem item in items)
        {
            string target = Path.Combine(outputFolder, item.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, item.Content);
        }

        return items.Count;
    }

    // Reconstroi o model.cfg em memoria (sem gravar em disco).
    public static TextItem GetModelCfg(string inputPath, Action<string> log)
    {
        object odol = LoadOrThrow(inputPath, log);

        string modelName = Path.GetFileNameWithoutExtension(inputPath);
        string[] lines = BuildModelCfg(odol, modelName, log);

        log(Strings.T("Core.LogModelCfgLines", lines.Length));
        return new TextItem(modelName + ".cfg", string.Join(Environment.NewLine, lines));
    }

    public static string ExtractModelCfg(string inputPath, string outputFolder, Action<string> log)
    {
        TextItem item = GetModelCfg(inputPath, log);

        string target = Path.Combine(outputFolder, item.Name);
        File.WriteAllText(target, item.Content);
        return target;
    }

    // Reconstroi um model.cfg com CfgSkeletons (ossos/hierarquia) e CfgModels
    // (skeletonName + sections[]) a partir dos dados do ODOL. Sem o bloco de
    // animacoes, pois o ODOL nao guarda esses dados de forma 100% fiel.
    private static string[] BuildModelCfg(object odol, string modelName, Action<string> log)
    {
        object? skeleton = GetMember(odol, "Skeleton");

        string skelName  = GetMember(skeleton, "Name") as string ?? string.Empty;
        bool isDiscrete  = GetMember(skeleton, "isDiscrete") as bool? ?? false;
        string[] bones   = GetMember(skeleton, "bones") as string[] ?? Array.Empty<string>();
        string pivots    = GetMember(skeleton, "pivotsNameObsolete") as string ?? string.Empty;

        if (string.IsNullOrWhiteSpace(skelName))
            skelName = bones.Length > 0 ? modelName + "_skeleton" : string.Empty;

        List<string> sections = CollectSections(odol);

        log(Strings.T("Core.LogSkeleton", skelName, bones.Length / 2, sections.Count));

        var o = new List<string>
        {
            "// model.cfg reconstruido por P3D Debinarizer",
            "// Contem CfgSkeletons, CfgModels e Animations (reconstruidos do ODOL).",
            string.Empty,
            "class CfgSkeletons",
            "{"
        };

        if (skelName.Length > 0)
        {
            o.Add($"\tclass {skelName}");
            o.Add("\t{");
            o.Add($"\t\tisDiscrete = {(isDiscrete ? 1 : 0)};");
            o.Add("\t\tskeletonInherit = \"\";");
            o.Add("\t\tskeletonBones[] =");
            o.Add("\t\t{");
            for (int i = 0; i + 1 < bones.Length; i += 2)
            {
                string comma = i + 2 < bones.Length ? "," : string.Empty;
                o.Add($"\t\t\t\"{bones[i]}\", \"{bones[i + 1]}\"{comma}");
            }
            o.Add("\t\t};");
            if (!string.IsNullOrWhiteSpace(pivots))
                o.Add($"\t\tpivotsModel = \"{pivots}\";");
            o.Add("\t};");
        }

        o.Add("};");
        o.Add(string.Empty);
        o.Add("class CfgModels");
        o.Add("{");
        o.Add($"\tclass {modelName}");
        o.Add("\t{");
        o.Add($"\t\tskeletonName = \"{skelName}\";");
        o.Add("\t\tsectionsInherit = \"\";");

        if (sections.Count > 0)
        {
            o.Add("\t\tsections[] =");
            o.Add("\t\t{");
            for (int i = 0; i < sections.Count; i++)
            {
                string comma = i + 1 < sections.Count ? "," : string.Empty;
                o.Add($"\t\t\t\"{sections[i]}\"{comma}");
            }
            o.Add("\t\t};");
        }
        else
        {
            o.Add("\t\tsections[] = {};");
        }

        List<string> animBlock = BuildAnimationsBlock(odol, bones, log);
        if (animBlock.Count > 0)
            o.AddRange(animBlock);

        o.Add("\t};");
        o.Add("};");

        return o.ToArray();
    }

    // Reconstroi o bloco class Animations a partir de odol.animations.animationClasses.
    // Cada AnimationClass guarda: animName, animType, animSource, sourceAddress,
    // minValue/maxValue, angle0/1 (rotacao), offset0/1 (translacao), hideValue,
    // animPeriod, initPhase, axisPos e axisDir. A selecao (bone) vem de Anims2Bones.
    // O nome do "axis" (selecao no Memory LOD) e resolvido por casamento de
    // coordenadas: vertice da named selection ~= axisPos; se nao houver
    // memory LOD acessivel, cai para a convencao "<bone>_axis".
    private static List<string> BuildAnimationsBlock(object odol, string[] bones, Action<string> log)
    {
        var lines = new List<string>();

        object? animations = GetMember(odol, "animations") ?? GetMember(odol, "Animations");
        if (animations == null)
            return lines;

        if (GetMember(animations, "animationClasses") is not Array classes || classes.Length == 0)
            return lines;

        int[][]? anims2Bones = GetMember(animations, "Anims2Bones") as int[][];

        // Mapeia coordenadas -> nome de selecao do Memory LOD para resolver axis.
        var axisLookup = BuildMemoryAxisLookup(odol);

        lines.Add(string.Empty);
        lines.Add("\t\tclass Animations");
        lines.Add("\t\t{");

        int animCount = 0;
        for (int i = 0; i < classes.Length; i++)
        {
            object? cls = classes.GetValue(i);
            if (cls == null)
                continue;

            string animName = GetMember(cls, "animName") as string ?? $"anim_{i}";
            if (string.IsNullOrWhiteSpace(animName))
                animName = $"anim_{i}";
            animName = SanitizeClassName(animName);

            string typeStr   = MapAnimType(GetMember(cls, "animType"));
            string srcAddr   = MapAnimAddress(GetMember(cls, "sourceAddress"));
            string source    = GetMember(cls, "animSource") as string ?? string.Empty;
            float  minValue  = GetSingle(cls, "minValue");
            float  maxValue  = GetSingle(cls, "maxValue");
            float  animPer   = GetSingle(cls, "animPeriod");
            float  initPhase = GetSingle(cls, "initPhase");
            float  angle0    = GetSingle(cls, "angle0");
            float  angle1    = GetSingle(cls, "angle1");
            float  offset0   = GetSingle(cls, "offset0");
            float  offset1   = GetSingle(cls, "offset1");
            float  hideVal   = GetSingle(cls, "hideValue");

            string selection = GetSelectionForAnim(anims2Bones, i, bones);
            string axisName  = ResolveAxisName(GetMember(cls, "axisPos"), GetMember(cls, "axisDir"), axisLookup, selection);

            lines.Add($"\t\t\tclass {animName}");
            lines.Add("\t\t\t{");
            lines.Add($"\t\t\t\ttype = \"{typeStr}\";");
            lines.Add($"\t\t\t\tsource = \"{source}\";");
            if (selection.Length > 0)
                lines.Add($"\t\t\t\tselection = \"{selection}\";");
            if (NeedsAxis(typeStr) && axisName.Length > 0)
                lines.Add($"\t\t\t\taxis = \"{axisName}\";");
            lines.Add($"\t\t\t\tsourceAddress = \"{srcAddr}\";");
            lines.Add($"\t\t\t\tminValue = {Fmt(minValue)};");
            lines.Add($"\t\t\t\tmaxValue = {Fmt(maxValue)};");

            if (typeStr.StartsWith("rotation", StringComparison.Ordinal))
            {
                lines.Add($"\t\t\t\tangle0 = {Fmt(angle0)};");
                lines.Add($"\t\t\t\tangle1 = {Fmt(angle1)};");
            }
            else if (typeStr.StartsWith("translation", StringComparison.Ordinal))
            {
                lines.Add($"\t\t\t\toffset0 = {Fmt(offset0)};");
                lines.Add($"\t\t\t\toffset1 = {Fmt(offset1)};");
            }
            else if (typeStr == "hide")
            {
                lines.Add($"\t\t\t\thideValue = {Fmt(hideVal)};");
            }
            else if (typeStr == "direct")
            {
                lines.Add($"\t\t\t\tangle = {Fmt(GetSingle(cls, "angle"))};");
                lines.Add($"\t\t\t\taxisOffset = {Fmt(GetSingle(cls, "axisOffset"))};");
            }

            lines.Add($"\t\t\t\tanimPeriod = {Fmt(animPer)};");
            lines.Add($"\t\t\t\tinitPhase = {Fmt(initPhase)};");
            lines.Add("\t\t\t};");
            animCount++;
        }

        lines.Add("\t\t};");

        log(Strings.T("Core.LogAnimationsRebuilt", animCount));
        return lines;
    }

    private static bool NeedsAxis(string type)
        => type.StartsWith("rotation", StringComparison.Ordinal)
        || type.StartsWith("translation", StringComparison.Ordinal)
        || type == "direct";

    private static string MapAnimType(object? v)
    {
        if (v == null) return "rotation";
        string s = v.ToString() ?? "rotation";
        return s switch
        {
            "Rotation"      => "rotation",
            "RotationX"     => "rotationX",
            "RotationY"     => "rotationY",
            "RotationZ"     => "rotationZ",
            "Translation"   => "translation",
            "TranslationX"  => "translationX",
            "TranslationY"  => "translationY",
            "TranslationZ"  => "translationZ",
            "Direct"        => "direct",
            "Hide"          => "hide",
            _               => s.ToLowerInvariant()
        };
    }

    private static string MapAnimAddress(object? v)
    {
        if (v == null) return "clamp";
        string s = v.ToString() ?? "clamp";
        return s switch
        {
            "AnimClamp"  => "clamp",
            "AnimLoop"   => "loop",
            "AnimMirror" => "mirror",
            _            => "clamp"
        };
    }

    private static float GetSingle(object obj, string field)
    {
        object? v = GetMember(obj, field);
        return v is float f ? f : (v is double d ? (float)d : 0f);
    }

    private static string Fmt(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return "0";
        return v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }

    // Substitui caracteres invalidos em nomes de classe (animName as vezes vem
    // com espacos ou pontuacao). Mantem letras, digitos e underline.
    private static string SanitizeClassName(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        if (sb.Length > 0 && char.IsDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    // Recupera o nome do bone alvo da animacao via Anims2Bones[anim][lod].
    // bones e o array plano [filho,pai,filho,pai,...] do esqueleto; bone idx i
    // corresponde a bones[i*2].
    private static string GetSelectionForAnim(int[][]? anims2Bones, int animIdx, string[] bones)
    {
        if (anims2Bones == null || bones.Length == 0)
            return string.Empty;

        // Layout do ODOL: Anims2Bones[lod][animIdx] = indice do bone (ou -1).
        // Percorremos todos os LODs e ficamos com o primeiro bone valido.
        foreach (int[]? row in anims2Bones)
        {
            if (row == null || animIdx >= row.Length)
                continue;
            int b = row[animIdx];
            if (b >= 0 && b * 2 < bones.Length)
                return bones[b * 2];
        }

        return string.Empty;
    }

    // Lookup auxiliar: lista (nome, vertices) de cada named selection do
    // Memory LOD para resolver o nome do "axis" por coordenada.
    private sealed record AxisCandidate(string Name, float[][] Vertices);

    private static List<AxisCandidate> BuildMemoryAxisLookup(object odol)
    {
        var list = new List<AxisCandidate>();

        if (GetMember(odol, "lods") is not Array lods || lods.Length == 0)
            return list;

        // Tenta usar modelInfo.memory como indice; senao busca resolucao 1e13.
        object? modelInfo = GetMember(odol, "modelInfo");
        object? memIdxObj = GetMember(modelInfo, "memory");
        object? memLod = null;

        if (memIdxObj is byte mb && mb < lods.Length)
            memLod = lods.GetValue(mb);

        if (memLod == null)
        {
            foreach (object? lod in lods)
            {
                if (lod == null) continue;
                float res = GetSingle(lod, "resolution");
                if (res > 9e12f && res < 2e13f) { memLod = lod; break; }
            }
        }

        if (memLod == null)
            return list;

        if (GetMember(memLod, "vertices") is not Array vertsArr ||
            GetMember(memLod, "namedSelections") is not Array named)
            return list;

        // Pre-extrai vertices em array de floats[3].
        var verts = new float[vertsArr.Length][];
        for (int i = 0; i < vertsArr.Length; i++)
        {
            object? v = vertsArr.GetValue(i);
            verts[i] = new[] { GetSingle(v!, "X"), GetSingle(v!, "Y"), GetSingle(v!, "Z") };
        }

        foreach (object? ns in named)
        {
            if (ns == null) continue;
            string? name = GetMember(ns, "Name") as string;
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (GetMember(ns, "SelectedVertices") is not Array sv) continue;

            var coords = new List<float[]>(sv.Length);
            foreach (object? vi in sv)
            {
                if (vi == null) continue;
                int idx = GetMember(vi, "value") as int? ?? -1;
                if (idx >= 0 && idx < verts.Length)
                    coords.Add(verts[idx]);
            }
            if (coords.Count > 0)
                list.Add(new AxisCandidate(name!, coords.ToArray()));
        }

        return list;
    }

    private static string ResolveAxisName(object? axisPos, object? axisDir, List<AxisCandidate> lookup, string fallbackSelection)
    {
        string fb = string.IsNullOrEmpty(fallbackSelection) ? string.Empty : fallbackSelection + "_axis";

        if (axisPos == null || lookup.Count == 0)
            return fb;

        float px = GetSingle(axisPos, "X");
        float py = GetSingle(axisPos, "Y");
        float pz = GetSingle(axisPos, "Z");

        const float tol = 1e-3f;

        foreach (AxisCandidate c in lookup)
        {
            foreach (float[] v in c.Vertices)
            {
                if (System.Math.Abs(v[0] - px) < tol &&
                    System.Math.Abs(v[1] - py) < tol &&
                    System.Math.Abs(v[2] - pz) < tol)
                    return c.Name;
            }
        }

        return fb;
    }

    // Coleta os nomes de selecoes nomeadas marcadas como sectional em todos os LODs.
    private static List<string> CollectSections(object odol)
    {
        var sections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (GetMember(odol, "lods") is not Array lods)
            return sections;

        foreach (object? lod in lods)
        {
            if (lod == null)
                continue;

            if (GetMember(lod, "namedSelections") is not Array named)
                continue;

            foreach (object? ns in named)
            {
                if (ns == null)
                    continue;

                bool sectional = GetMember(ns, "IsSectional") as bool? ?? false;
                string? name = GetMember(ns, "Name") as string;

                if (sectional && !string.IsNullOrWhiteSpace(name) && seen.Add(name))
                    sections.Add(name);
            }
        }

        return sections;
    }

    private static object? GetMember(object? target, string name)
    {
        if (target == null)
            return null;

        Type t = target.GetType();
        var field = t.GetField(name, MemberFlags);
        if (field != null)
            return field.GetValue(target);

        var prop = t.GetProperty(name, MemberFlags);
        return prop?.GetValue(target);
    }

    // Invoca via reflexao, desembrulhando TargetInvocationException para
    // expor a mensagem real do erro.
    private static object? Invoke(MethodInfo method, object? target, params object?[] args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static object LoadOrThrow(string inputPath, Action<string> log)
        => Converter.LoadOdol(inputPath, log)
           ?? throw new InvalidOperationException(Strings.T("Core.ExCannotLoadOdol"));

    // Converte um caminho interno do Arma ("mod\data\x.rvmat") em caminho
    // relativo seguro, removendo drive: e barras iniciais.
    private static string RelativeSafePath(string armaPath, string forcedExtension)
    {
        string p = armaPath.Replace('/', '\\').Trim();

        int colon = p.IndexOf(':');
        if (colon >= 0)
            p = p[(colon + 1)..];

        p = p.TrimStart('\\');

        if (string.IsNullOrWhiteSpace(p))
            p = "material" + forcedExtension;

        if (!Path.HasExtension(p))
            p += forcedExtension;

        string[] segments = p.Split('\\', StringSplitOptions.RemoveEmptyEntries)
                             .Select(MakeSafeSegment)
                             .ToArray();

        return Path.Combine(segments);
    }

    private static string MakeSafeSegment(string segment)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(segment.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
