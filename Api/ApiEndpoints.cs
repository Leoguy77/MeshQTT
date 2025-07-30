using Microsoft.AspNetCore.Mvc;
using MeshQTT.Api.Models;
using MeshQTT.Api.Services;

namespace MeshQTT.Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class MeshQTTController : ControllerBase
    {
        private readonly ApiService apiService;

        public MeshQTTController(ApiService apiService)
        {
            this.apiService = apiService;
        }

        /// <summary>
        /// Get all connected nodes
        /// </summary>
        [HttpGet("nodes")]
        public IActionResult GetNodes()
        {
            try
            {
                var nodes = apiService.GetAllNodes();
                return Ok(new ApiResponse<List<NodeResponse>>
                {
                    Success = true,
                    Message = "Nodes retrieved successfully",
                    Data = nodes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error retrieving nodes: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get a specific node by ID
        /// </summary>
        [HttpGet("nodes/{nodeId}")]
        public IActionResult GetNode(string nodeId)
        {
            try
            {
                var node = apiService.GetNode(nodeId);
                if (node == null)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = "Node not found"
                    });
                }

                return Ok(new ApiResponse<NodeResponse>
                {
                    Success = true,
                    Message = "Node retrieved successfully",
                    Data = node
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error retrieving node: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Ban a node
        /// </summary>
        [HttpPost("nodes/{nodeId}/ban")]
        public async Task<IActionResult> BanNode(string nodeId, [FromBody] NodeBanRequest? request)
        {
            try
            {
                var reason = request?.Reason ?? "Banned via API";
                var success = await apiService.BanNodeAsync(nodeId, reason);
                
                if (success)
                {
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"Node {nodeId} has been banned"
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Node is already banned or could not be banned"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error banning node: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Unban a node
        /// </summary>
        [HttpDelete("nodes/{nodeId}/ban")]
        public IActionResult UnbanNode(string nodeId)
        {
            try
            {
                var success = apiService.UnbanNode(nodeId);
                
                if (success)
                {
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"Node {nodeId} has been unbanned"
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Node was not banned or could not be unbanned"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error unbanning node: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get current configuration
        /// </summary>
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            try
            {
                var config = apiService.GetConfig();
                if (config == null)
                {
                    return StatusCode(500, new ApiResponse
                    {
                        Success = false,
                        Message = "Configuration not available"
                    });
                }

                return Ok(new ApiResponse<ConfigResponse>
                {
                    Success = true,
                    Message = "Configuration retrieved successfully",
                    Data = config
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error retrieving configuration: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Update configuration settings
        /// </summary>
        [HttpPut("config")]
        public IActionResult UpdateConfig([FromBody] ConfigUpdateRequest request)
        {
            try
            {
                var success = apiService.UpdateConfig(request);
                
                if (success)
                {
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = "Configuration updated successfully"
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "No configuration changes were made"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error updating configuration: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get system statistics
        /// </summary>
        [HttpGet("stats")]
        public IActionResult GetSystemStats()
        {
            try
            {
                var stats = apiService.GetSystemStats();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "System statistics retrieved successfully",
                    Data = stats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Error retrieving system statistics: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new ApiResponse
            {
                Success = true,
                Message = "MeshQTT API is healthy"
            });
        }
    }
}