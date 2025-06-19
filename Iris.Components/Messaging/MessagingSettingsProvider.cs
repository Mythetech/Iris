using Iris.Components.Settings;

namespace Iris.Components.Messaging;

public class MessagingSettingsProvider : ISettingsSectionProvider
{
    public Section GetSection()
    {
        return new MessagingSettingsSection();
    }
}