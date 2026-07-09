using Scriban;
using Scriban.Runtime;

namespace ArchitectLuna.Templates;

/// <summary>
/// Thin wrapper over Scriban that renders a .NET view model against a template using snake_case
/// member names (e.g. a "CommandName" property is referenced as "{{ command_name }}").
/// </summary>
public sealed class TemplateEngine
{
    public string Render(string templateText, string templateName, object model)
    {
        var template = Template.Parse(templateText, templateName);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Template '{templateName}' failed to parse: {string.Join("; ", template.Messages)}");
        }

        var context = new TemplateContext
        {
            MemberRenamer = StandardMemberRenamer.Rename,
            LoopLimit = 100_000,
        };

        var scriptObject = new ScriptObject();
        scriptObject.Import(model, renamer: StandardMemberRenamer.Rename);
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }
}
