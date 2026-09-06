namespace WindowsTaskViewFSE.Services;

public interface ISoundService
{
    bool IsMuted { get; set; }
    void PlayNavigate();
    void PlaySelect();
    void PlayBack();
    void PlayClose();
    void PlayNotification();
}
