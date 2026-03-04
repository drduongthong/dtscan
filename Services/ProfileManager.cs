using System.Text.Json;
using DTScan.Models;

namespace DTScan.Services;

public class ProfileManager
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DTScan");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "profiles.json");

    public List<UserProfile> Profiles { get; private set; } = [];
    public UserProfile ActiveProfile { get; set; } = null!;

    public void Load()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        if (File.Exists(ConfigFile))
        {
            try
            {
                var json = File.ReadAllText(ConfigFile);
                var data = JsonSerializer.Deserialize<ProfileData>(json);
                if (data is { Profiles.Count: > 0 })
                {
                    Profiles = data.Profiles;
                    ActiveProfile = Profiles.FirstOrDefault(p => p.Id == data.ActiveProfileId)
                                   ?? Profiles[0];
                    return;
                }
            }
            catch { /* corrupt config – recreate */ }
        }

        var defaultProfile = new UserProfile();
        Profiles.Add(defaultProfile);
        ActiveProfile = defaultProfile;
        Save();
    }

    public void Save()
    {
        var data = new ProfileData
        {
            Profiles = Profiles,
            ActiveProfileId = ActiveProfile.Id
        };
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(data, opts));
    }

    public UserProfile CreateProfile(string name)
    {
        var profile = new UserProfile { Name = name };
        Profiles.Add(profile);
        Save();
        return profile;
    }

    public void DeleteProfile(UserProfile profile)
    {
        if (Profiles.Count <= 1) return;
        Profiles.Remove(profile);
        if (ActiveProfile.Id == profile.Id)
            ActiveProfile = Profiles[0];
        Save();
    }

    private sealed class ProfileData
    {
        public List<UserProfile> Profiles { get; set; } = [];
        public string ActiveProfileId { get; set; } = "";
    }
}
