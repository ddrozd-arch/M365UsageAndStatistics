public class FormsService
{
    public async Task<string> GetFormsUsage()
    {
        await Task.Delay(100);
        return "date,user,forms\n2026-04-26,test,5";
    }
}
