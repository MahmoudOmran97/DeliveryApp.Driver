namespace DeliveryApp.Driver.Services;

public class AuthService
{
    private const string K_Token = "driver_token";
    private const string K_Id = "driver_user_id";
    private const string K_Name = "driver_user_name";
    private const string K_Email = "driver_user_email";

    public bool IsLoggedIn => !string.IsNullOrEmpty(Preferences.Get(K_Token, null));

    public void SaveUser(string token, int id, string name, string email)
    {
        Preferences.Set(K_Token, token);
        Preferences.Set(K_Id, id);
        Preferences.Set(K_Name, name);
        Preferences.Set(K_Email, email);
    }

    public string GetToken() => Preferences.Get(K_Token, string.Empty);
    public int GetUserId() => Preferences.Get(K_Id, 0);
    public string GetUserName() => Preferences.Get(K_Name, string.Empty);
    public string GetEmail() => Preferences.Get(K_Email, string.Empty);

    public void Logout()
    {
        Preferences.Remove(K_Token);
        Preferences.Remove(K_Id);
        Preferences.Remove(K_Name);
        Preferences.Remove(K_Email);
    }
}