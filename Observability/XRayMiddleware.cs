using Amazon.XRay.Recorder.Core;

namespace ms_games.Observability
{
  public class XRayMiddleware
  {
    private readonly RequestDelegate _next;

    public XRayMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
      AWSXRayRecorder.Instance.BeginSegment("GamesAPI");

      try
      {
        await _next(context);
      }
      catch (Exception ex)
      {
        AWSXRayRecorder.Instance.AddException(ex);
        throw;
      }
      finally
      {
        AWSXRayRecorder.Instance.EndSegment();
      }
    }
  }
}
