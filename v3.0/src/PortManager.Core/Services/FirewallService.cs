using NewLife.Log;
using PortManager.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PortManager.Services;

/// <summary>
/// 防火墙规则检测服务
/// </summary>
public class FirewallService
{
    private List<FirewallRuleDetail>? _cachedInboundRules;
    private List<FirewallRuleDetail>? _cachedOutboundRules;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheValidDuration = TimeSpan.FromMinutes(5); // 缓存5分钟
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// 缓存刷新完成事件
    /// </summary>
    public event EventHandler? CacheRefreshed;

    /// <summary>
    /// 获取端口的防火墙规则信息
    /// </summary>
    public PortFirewallInfo? GetPortFirewallInfo(int port, string protocol = "TCP")
    {
        try
        {
            var info = new PortFirewallInfo();
            
            // 使用缓存的规则
            var inboundRules = GetCachedInboundRules();
            var outboundRules = GetCachedOutboundRules();
            
            XTrace.WriteLine($"Port {port}: {inboundRules.Count} inbound rules, {outboundRules.Count} outbound rules in cache");
            
            // 查找匹配的入站规则
            var matchingInbound = inboundRules.Where(r => RuleMatchesPort(r, port)).ToList();
            XTrace.WriteLine($"Port {port}: {matchingInbound.Count} matching inbound rules");
            if (matchingInbound.Count > 0)
            {
                info.AllowInbound = matchingInbound.Any(r => r.Action == "Allow" && r.Enabled == "True");
                info.InboundRuleName = matchingInbound.FirstOrDefault(r => r.Action == "Allow")?.Name 
                    ?? matchingInbound.FirstOrDefault()?.Name;
                XTrace.WriteLine($"Port {port}: AllowInbound={info.AllowInbound}, RuleName={info.InboundRuleName}");
            }
            else
            {
                info.AllowInbound = IsDefaultInboundAllowed();
                XTrace.WriteLine($"Port {port}: No inbound rules, using default={info.AllowInbound}");
            }
            
            // 查找匹配的出站规则
            var matchingOutbound = outboundRules.Where(r => RuleMatchesPort(r, port)).ToList();
            XTrace.WriteLine($"Port {port}: {matchingOutbound.Count} matching outbound rules");
            if (matchingOutbound.Count > 0)
            {
                info.AllowOutbound = matchingOutbound.Any(r => r.Action == "Allow" && r.Enabled == "True");
                info.OutboundRuleName = matchingOutbound.FirstOrDefault(r => r.Action == "Allow")?.Name 
                    ?? matchingOutbound.FirstOrDefault()?.Name;
            }
            else
            {
                info.AllowOutbound = IsDefaultOutboundAllowed();
            }
            
            return info;
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug($"Error getting firewall info for port {port}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 批量获取多个端口的防火墙信息
    /// </summary>
    public Dictionary<int, PortFirewallInfo> GetPortsFirewallInfo(List<int> ports, string protocol = "TCP")
    {
        var result = new Dictionary<int, PortFirewallInfo>();
        
        try
        {
            // 使用缓存的规则
            var allInboundRules = GetCachedInboundRules();
            var allOutboundRules = GetCachedOutboundRules();
            
            var defaultInboundAllowed = IsDefaultInboundAllowed();
            var defaultOutboundAllowed = IsDefaultOutboundAllowed();
            
            foreach (var port in ports.Distinct())
            {
                var info = new PortFirewallInfo();
                
                // 查找匹配的入站规则
                var inboundRules = allInboundRules.Where(r => RuleMatchesPort(r, port)).ToList();
                if (inboundRules.Count > 0)
                {
                    info.AllowInbound = inboundRules.Any(r => r.Action == "Allow" && r.Enabled == "True");
                    info.InboundRuleName = inboundRules.FirstOrDefault(r => r.Action == "Allow")?.Name 
                        ?? inboundRules.FirstOrDefault()?.Name;
                }
                else
                {
                    info.AllowInbound = defaultInboundAllowed;
                }
                
                // 查找匹配的出站规则
                var outboundRules = allOutboundRules.Where(r => RuleMatchesPort(r, port)).ToList();
                if (outboundRules.Count > 0)
                {
                    info.AllowOutbound = outboundRules.Any(r => r.Action == "Allow" && r.Enabled == "True");
                    info.OutboundRuleName = outboundRules.FirstOrDefault(r => r.Action == "Allow")?.Name 
                        ?? outboundRules.FirstOrDefault()?.Name;
                }
                else
                {
                    info.AllowOutbound = defaultOutboundAllowed;
                }
                
                result[port] = info;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error getting firewall info for ports: {ex.Message}");
        }
        
        return result;
    }
    
    /// <summary>
    /// 获取缓存的入站规则
    /// </summary>
    private List<FirewallRuleDetail> GetCachedInboundRules()
    {
        if (_cachedInboundRules != null && DateTime.Now - _lastCacheTime < _cacheValidDuration)
        {
            return _cachedInboundRules;
        }
        
        // 刷新缓存
        RefreshCacheIfNeeded();
        
        return _cachedInboundRules ?? new List<FirewallRuleDetail>();
    }
    
    /// <summary>
    /// 获取缓存的出站规则
    /// </summary>
    private List<FirewallRuleDetail> GetCachedOutboundRules()
    {
        if (_cachedOutboundRules != null && DateTime.Now - _lastCacheTime < _cacheValidDuration)
        {
            return _cachedOutboundRules;
        }
        
        // 刷新缓存
        RefreshCacheIfNeeded();
        
        return _cachedOutboundRules ?? new List<FirewallRuleDetail>();
    }
    
    /// <summary>
    /// 刷新缓存（如果需要）
    /// </summary>
    private void RefreshCacheIfNeeded()
    {
        // 使用简单的标志来避免并发执行
        if (_isRefreshing)
        {
            return;
        }
        
        _isRefreshing = true;
        
        try
        {
            // 再次检查缓存是否有效
            if (_cachedInboundRules != null && _cachedOutboundRules != null && 
                DateTime.Now - _lastCacheTime < _cacheValidDuration)
            {
                return;
            }
            
            XTrace.WriteLine("Refreshing firewall rules cache...");
            
            // 查询入站规则
            var inboundRules = QueryAllFirewallRulesWithPowerShell(true);
            
            // 查询出站规则
            var outboundRules = QueryAllFirewallRulesWithPowerShell(false);
            
            if (inboundRules.Count > 0 || outboundRules.Count > 0)
            {
                _cachedInboundRules = inboundRules;
                _cachedOutboundRules = outboundRules;
                _lastCacheTime = DateTime.Now;
                XTrace.WriteLine($"Firewall rules cache refreshed: {inboundRules.Count} inbound, {outboundRules.Count} outbound");
                
                // 触发缓存刷新完成事件
                CacheRefreshed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error refreshing cache: {ex.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }
    
    private bool _isRefreshing = false;
    
    /// <summary>
    /// 异步刷新缓存
    /// </summary>
    private async Task RefreshCacheAsync()
    {
        if (!await _cacheLock.WaitAsync(0))
        {
            // 另一个线程正在刷新，跳过
            return;
        }
        
        try
        {
            // 再次检查缓存是否有效
            if (_cachedInboundRules != null && _cachedOutboundRules != null && 
                DateTime.Now - _lastCacheTime < _cacheValidDuration)
            {
                return;
            }
            
            XTrace.Log.Debug("Refreshing firewall rules cache...");
            
            // 在后台线程执行 PowerShell 查询
            await Task.Run(() =>
            {
                try
                {
                    var inboundRules = QueryAllFirewallRulesWithPowerShell(true);
                    var outboundRules = QueryAllFirewallRulesWithPowerShell(false);
                    
                    _cachedInboundRules = inboundRules;
                    _cachedOutboundRules = outboundRules;
                    _lastCacheTime = DateTime.Now;
                    
                    XTrace.Log.Debug($"Firewall rules cache refreshed: {inboundRules.Count} inbound, {outboundRules.Count} outbound");
                    
                    // 触发缓存刷新完成事件
                    CacheRefreshed?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug($"Failed to refresh firewall cache: {ex.Message}");
                }
            });
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    /// <summary>
    /// 使用 PowerShell 批量查询所有防火墙规则
    /// </summary>
    private List<FirewallRuleDetail> QueryAllFirewallRulesWithPowerShell(bool isInbound)
    {
        try
        {
            var direction = isInbound ? "in" : "out";
            var rules = QueryFirewallRulesWithNetsh(direction);
            return rules;
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error querying firewall rules: {ex.Message}");
        }
        
        return new List<FirewallRuleDetail>();
    }
    
    /// <summary>
    /// 使用 netsh 查询防火墙规则（更快的替代方案）
    /// </summary>
    private List<FirewallRuleDetail> QueryFirewallRulesWithNetsh(string direction)
    {
        var rules = new List<FirewallRuleDetail>();
        
        try
        {
            // 使用 cmd 执行 netsh 命令，并设置代码页为 UTF-8
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c chcp 65001 > nul && netsh advfirewall firewall show rule name=all dir={direction}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return rules;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            
            rules = ParseNetshOutput(output, direction);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error in QueryFirewallRulesWithNetsh: {ex.Message}");
        }
        
        return rules;
    }
    
    /// <summary>
    /// 解析 netsh 输出
    /// </summary>
    private List<FirewallRuleDetail> ParseNetshOutput(string output, string direction)
    {
        var rules = new List<FirewallRuleDetail>();
        
        if (string.IsNullOrEmpty(output))
            return rules;
        
        try
        {
            var lines = output.Split('\n');
            FirewallRuleDetail? currentRule = null;
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // 规则名称行 (支持中英文和全角/半角冒号)
                // 使用 Contains 而不是 StartsWith，因为 netsh 输出格式可能有变化
                if (trimmedLine.Contains("Rule Name:") || trimmedLine.Contains("规则名称:"))
                {
                    if (currentRule != null && !string.IsNullOrEmpty(currentRule.Name))
                    {
                        rules.Add(currentRule);
                    }
                    currentRule = new FirewallRuleDetail
                    {
                        Direction = direction == "in" ? "Inbound" : "Outbound",
                        Enabled = "True", // netsh 只显示启用的规则
                        LocalPort = "Any",
                        Protocol = "Any"
                    };
                    // 找到冒号的位置
                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        currentRule.Name = trimmedLine.Substring(colonIndex + 1).Trim();
                    }
                }
                // 操作 (支持中英文和全角/半角冒号)
                else if (currentRule != null && (trimmedLine.Contains("Action:") || trimmedLine.Contains("操作:")))
                {
                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        var action = trimmedLine.Substring(colonIndex + 1).Trim();
                        // 转换中文操作到英文
                        currentRule.Action = action switch
                        {
                            "允许" => "Allow",
                            "阻止" => "Block",
                            _ => action
                        };
                    }
                }
                // 协议 (支持中英文和全角/半角冒号)
                else if (currentRule != null && (trimmedLine.Contains("Protocol:") || trimmedLine.Contains("协议:")))
                {
                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        currentRule.Protocol = trimmedLine.Substring(colonIndex + 1).Trim();
                    }
                }
                // 本地端口 (支持中英文和全角/半角冒号)
                else if (currentRule != null && (trimmedLine.Contains("LocalPort:") || trimmedLine.Contains("本地端口:")))
                {
                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        currentRule.LocalPort = trimmedLine.Substring(colonIndex + 1).Trim();
                    }
                }
            }
            
            // 添加最后一条规则
            if (currentRule != null && !string.IsNullOrEmpty(currentRule.Name))
            {
                rules.Add(currentRule);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error parsing netsh output: {ex.Message}");
        }
        
        return rules;
    }
    
    /// <summary>
    /// 执行 PowerShell 脚本（带超时）
    /// </summary>
    private string ExecutePowerShellScript(string script)
    {
        try
        {
            XTrace.WriteLine("ExecutePowerShellScript: Starting...");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\"\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process == null) 
            {
                XTrace.WriteLine("ExecutePowerShellScript: Failed to start process");
                return string.Empty;
            }

            XTrace.WriteLine("ExecutePowerShellScript: Process started, waiting for output...");
            
            // 使用异步读取避免阻塞
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            // 等待任务完成或超时（30秒）
            var completed = Task.WaitAll(new[] { outputTask, errorTask }, TimeSpan.FromSeconds(30));
            
            if (!completed)
            {
                XTrace.WriteLine("ExecutePowerShellScript: Timeout waiting for output after 30s");
                try { process.Kill(); } catch { }
                return string.Empty;
            }
            
            var output = outputTask.Result;
            var error = errorTask.Result;
            
            XTrace.WriteLine($"ExecutePowerShellScript: Output length={output.Length}, Error={error}");
            
            process.WaitForExit(1000); // 额外等待进程退出

            if (!string.IsNullOrEmpty(error))
            {
                XTrace.WriteLine($"PowerShell error: {error}");
            }

            XTrace.WriteLine("ExecutePowerShellScript: Completed successfully");
            return output.Trim();
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error executing PowerShell script: {ex.Message}");
            return string.Empty;
        }
    }
    
    /// <summary>
    /// 解析详细的 PowerShell 输出
    /// </summary>
    private List<FirewallRuleDetail> ParsePowerShellOutputDetail(string output)
    {
        var rules = new List<FirewallRuleDetail>();
        XTrace.WriteLine("=== ParsePowerShellOutputDetail ENTER ===");
        
        try
        {
            if (string.IsNullOrEmpty(output) || output == "null") 
            {
                XTrace.WriteLine("ParsePowerShellOutputDetail: output is null or empty");
                return rules;
            }
            
            XTrace.WriteLine($"ParsePowerShellOutputDetail: output starts with '{output[0]}', length={output.Length}");
            
            if (output.StartsWith("{"))
            {
                XTrace.WriteLine("Parsing single object format...");
                var rule = JsonSerializer.Deserialize<FirewallRuleDetail>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (rule != null) rules.Add(rule);
            }
            else if (output.StartsWith("["))
            {
                XTrace.WriteLine("Parsing array format...");
                try
                {
                    var ruleList = JsonSerializer.Deserialize<List<FirewallRuleDetail>>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (ruleList != null) 
                    {
                        rules.AddRange(ruleList);
                        XTrace.WriteLine($"Parsed {ruleList.Count} rules from array");
                    }
                    else
                    {
                        XTrace.WriteLine("ruleList is null after deserialization");
                    }
                }
                catch (JsonException jsonEx)
                {
                    XTrace.WriteLine($"JSON parsing error: {jsonEx.Message}");
                    XTrace.WriteLine($"JSON path: {jsonEx.Path}");
                }
            }
            else
            {
                XTrace.WriteLine($"Unexpected output format: {output.Substring(0, Math.Min(100, output.Length))}");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error parsing PowerShell output detail: {ex.Message}");
            XTrace.WriteLine($"Exception type: {ex.GetType().Name}");
        }
        
        XTrace.WriteLine($"=== ParsePowerShellOutputDetail EXIT with {rules.Count} rules ===");
        return rules;
    }
    
    /// <summary>
    /// 检查规则是否匹配指定端口
    /// </summary>
    private bool RuleMatchesPort(FirewallRuleDetail rule, int port)
    {
        if (rule.LocalPort == null) return false;
        
        // 检查是否是 "Any"
        if (rule.LocalPort.Equals("Any", StringComparison.OrdinalIgnoreCase)) return true;
        
        // 处理数组格式 (例如 "[22,22]" 或 "[\"22\",\"22\"]")
        var localPort = rule.LocalPort.Trim();
        if (localPort.StartsWith("[") && localPort.EndsWith("]"))
        {
            // 移除方括号并解析数组元素
            var content = localPort.Substring(1, localPort.Length - 2);
            var ports = content.Split(',').Select(p => p.Trim().Trim('\"'));
            return ports.Any(p => int.TryParse(p, out var pNum) && pNum == port);
        }
        
        // 检查单个端口
        if (int.TryParse(localPort, out var rulePort))
        {
            return rulePort == port;
        }
        
        // 检查端口范围 (例如 "80-100")
        if (localPort.Contains("-"))
        {
            var parts = localPort.Split('-');
            if (parts.Length == 2 && 
                int.TryParse(parts[0], out var start) && 
                int.TryParse(parts[1], out var end))
            {
                return port >= start && port <= end;
            }
        }
        
        // 检查逗号分隔的端口列表
        if (localPort.Contains(","))
        {
            var ports = localPort.Split(',').Select(p => p.Trim());
            return ports.Any(p => int.TryParse(p, out var pNum) && pNum == port);
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查默认入站行为是否允许
    /// </summary>
    private bool IsDefaultInboundAllowed()
    {
        // Windows 默认入站是阻止的，Linux 默认通常是允许的
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? false : true;
    }
    
    /// <summary>
    /// 检查默认出站行为是否允许
    /// </summary>
    private bool IsDefaultOutboundAllowed()
    {
        // Windows 和 Linux 默认出站都是允许的
        return true;
    }
    
    /// <summary>
    /// 根据绑定地址判断端口方向（用于 Linux）
    /// </summary>
    public static PortAccessDirection GetDirectionFromAddress(string address)
    {
        // 空地址或通配符地址表示监听所有接口，是入站
        if (string.IsNullOrEmpty(address) || 
            address == "0.0.0.0" || 
            address == "::" ||
            address == "*")
        {
            return PortAccessDirection.Inbound;
        }
        
        // 本地回环地址
        if (address == "127.0.0.1" || address == "::1" || address == "localhost")
        {
            // 本地通信，通常认为是双向或内部
            return PortAccessDirection.Bidirectional;
        }
        
        // 特定 IP 地址表示监听特定接口，是入站
        // 检查是否是有效的 IP 地址
        if (System.Net.IPAddress.TryParse(address, out _))
        {
            return PortAccessDirection.Inbound;
        }
        
        return PortAccessDirection.Unknown;
    }
}

/// <summary>
/// 防火墙规则基本信息
/// </summary>
public class FirewallRule
{
    public string Name { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Enabled { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
}

/// <summary>
/// 防火墙规则详细信息
/// </summary>
public class FirewallRuleDetail : FirewallRule
{
    private string? _localPort;
    
    [System.Text.Json.Serialization.JsonPropertyName("LocalPort")]
    [System.Text.Json.Serialization.JsonConverter(typeof(LocalPortConverter))]
    public string? LocalPort 
    { 
        get => _localPort;
        set => _localPort = value;
    }
    
    public string? Protocol { get; set; }
}

/// <summary>
/// 自定义转换器处理 LocalPort 可能是字符串、整数或数组的情况
/// </summary>
public class LocalPortConverter : System.Text.Json.Serialization.JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;
            case JsonTokenType.Number:
                return reader.GetInt32().ToString();
            case JsonTokenType.StartArray:
                var ports = new List<string>();
                // 读取数组中的每个元素
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;
                    if (reader.TokenType == JsonTokenType.String)
                        ports.Add(reader.GetString() ?? string.Empty);
                    else if (reader.TokenType == JsonTokenType.Number)
                        ports.Add(reader.GetInt32().ToString());
                }
                return string.Join(",", ports);
            default:
                // 跳过未知类型的值
                reader.Skip();
                return string.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

