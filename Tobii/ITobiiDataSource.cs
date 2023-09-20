namespace VRCFT_Tobii_Advanced.Tobii;

internal interface ITobiiDataSource : IDisposable
{
    void Subscribe();
    void Unsubscribe();
    void Update();
    EyeData GetEyeData();
}