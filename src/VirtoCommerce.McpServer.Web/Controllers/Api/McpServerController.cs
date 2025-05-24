using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.McpServer.Core;

namespace VirtoCommerce.McpServer.Web.Controllers.Api
{
    [Route("api/mcp-server")]
    public class McpServerController : Controller
    {
        // GET: api/mcp-server
        /// <summary>
        /// Get message
        /// </summary>
        /// <remarks>Return "Hello world!" message</remarks>
        [HttpGet]
        [Route("")]
        [Authorize(ModuleConstants.Security.Permissions.Read)]
        public ActionResult<string> Get()
        {
            return Ok(new { result = "Hello world!" });
        }
    }
}
