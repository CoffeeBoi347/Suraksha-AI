using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Suraksha.Auth;

public class SignOutButton : MonoBehaviour
{
    public Button _signOutButton;
    public UITweenerController _tweneerController;
    public Loading loading;

    private AuthService authService;

    private void Awake()
    {
        authService = new AuthService();
    }

    private void Start()
    {
        _signOutButton.onClick.AddListener(SignOut);
    }

    public void SignOut()
    {
        // Open confirmation popup
        _tweneerController.Init();
    }

    public async void ConfirmSignOut()
    {
        loading.Show();
        loading.SetText("Signing out...");
        authService.SignOut();

        await LoadMainSceneAsync();
    }

    public void Close()
    {
        _tweneerController.SetInactive();
    }

    private async Task LoadMainSceneAsync()
    {
        const string sceneName = "Authentication";

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        if (operation == null)
        {
            throw new System.Exception(
                $"Could not start loading scene: {sceneName}"
            );
        }

        while (!operation.isDone)
        {
            await Task.Yield();
        }
    }
}