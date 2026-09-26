using BookKnowledge.ServiceDefaults;
using BookKnowledge.BookSearch.Web;

var builder=WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app=builder.Build();
app.UseExceptionHandler(); app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseAntiforgery(); app.MapDefaultEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run(); public partial class Program { }
