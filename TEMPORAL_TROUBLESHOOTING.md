# Temporal gRPC Connection Troubleshooting Guide

This guide covers troubleshooting Temporal gRPC over HTTP/2 (h2c) connection issues in SwAIvyn, particularly the "broken pipes" problem that occurs when using Docker Swarm's routing mesh.

## Root Cause

Your BFF and Orchestrator are hitting Temporal through Swarm's routing mesh on port 7233. Temporal uses gRPC over HTTP/2 (h2c), and Swarm's ingress load-balancer on Docker Desktop often breaks h2c connections with broken pipes, even when the port reports as "ready".

## Applied Fixes

### ✅ 1. Host Network Mode for Temporal
**Status: IMPLEMENTED**

Temporal port is published with `mode: host` in `docker-stack.yml`:
```yaml
temporal:
  # ... other config
  ports:
    - target: 7233
      published: 7233
      protocol: tcp
      mode: host   # Bypasses routing mesh
```

This bypasses Docker Swarm's routing mesh entirely, eliminating h2c connection issues.

### ✅ 2. Force IPv4 Localhost
**Status: IMPLEMENTED**

In `scripts/dev-run.ps1`, services are configured to use IPv4:
```powershell
$serviceEnv = @{
    'TEMPORAL_HOST' = '127.0.0.1:7233'  # force IPv4
    # ... other config
}
```

This avoids IPv6 or DNS resolver issues.

### ✅ 3. Optional Traefik h2c Support
**Status: IMPLEMENTED**

Traefik labels added with h2c scheme support:
```yaml
temporal:
  # ... other config
  deploy:
    labels:
      - traefik.enable=true
      - traefik.http.routers.temporal.rule=Host(`temporal.localhost`)
      - traefik.http.routers.temporal.entrypoints=web
      - traefik.http.services.temporal.loadbalancer.server.port=7233
      - traefik.http.services.temporal.loadbalancer.server.scheme=h2c
```

If you prefer using Traefik, clients can connect to `temporal.localhost:80`.

### ✅ 4. Enhanced Healthcheck
**Status: IMPLEMENTED**

Added proper healthcheck to avoid "ready port, unready service" issues:
```yaml
temporal:
  # ... other config
  healthcheck:
    test: ["CMD-SHELL", "wget --quiet --tries=1 --spider http://localhost:8233/health || exit 1"]
    interval: 10s
    timeout: 3s
    retries: 10
```

### ✅ 5. PowerShell UTF-8 Fix
**Status: IMPLEMENTED**

Fixed the mojibake error in `scripts/dev-run.ps1` by setting UTF-8 encoding before emoji output.

## Validation Commands

### Temporal CLI Validation
If you have Temporal CLI (`tctl`) installed:
```powershell
# Check cluster health
tctl --address 127.0.0.1:7233 cluster health

# List namespaces
tctl --address 127.0.0.1:7233 namespace list
```

### grpcurl Validation
If you have `grpcurl` installed:
```powershell
# List available services
grpcurl -plaintext 127.0.0.1:7233 list

# Test system info endpoint
grpcurl -plaintext 127.0.0.1:7233 temporal.api.workflowservice.v1.WorkflowService/GetSystemInfo
```

### Docker Service Logs
Check Temporal service logs if validation fails:
```powershell
# Swarm service logs
docker service logs -f swaivyn_temporal

# Direct container logs
docker logs $(docker ps --filter name=swaivyn_temporal -q)
```

## Troubleshooting Steps

### 1. Verify Host Mode is Working
Check that Temporal is bound to the host port:
```powershell
# Should show Temporal listening on 0.0.0.0:7233 or *:7233
netstat -an | findstr :7233
```

### 2. Test Basic Connectivity
```powershell
# Test TCP connection
Test-NetConnection -ComputerName localhost -Port 7233

# Or using telnet
telnet localhost 7233
```

### 3. Check Container Status
```powershell
# List all containers with temporal in name
docker ps --filter name=temporal

# Check container details
docker inspect $(docker ps --filter name=swaivyn_temporal -q)
```

### 4. Windows Firewall Check
Ensure Windows Firewall isn't blocking port 7233:
```powershell
# Check firewall rules for port 7233
Get-NetFirewallRule | Where-Object { $_.DisplayName -like "*7233*" }
```

### 5. Control Test (Temporal Outside Swarm)
If problems persist, test Temporal outside Swarm:
```powershell
# Stop the current stack
docker stack rm swaivyn

# Run Temporal directly with host networking
docker run --rm -p 7233:7233 --network host temporalio/auto-setup:1.23
```

If clients work with this setup, it confirms that Swarm ingress was the issue.

## Expected Behavior After Fixes

1. **BFF and Orchestrator** should connect to Temporal at `127.0.0.1:7233` without broken pipe errors
2. **TCP connections** should be stable and persistent
3. **gRPC calls** should complete successfully without timeouts
4. **Service startup** should be reliable and consistent

## Additional Hardening

### Enhanced Service Waiting
The `scripts/dev-run.ps1` script includes enhanced Temporal readiness checking:
- Waits for TCP port availability
- Allows additional time for service initialization
- Verifies container is running
- Provides detailed status feedback

### Manager Node Constraint
Temporal is constrained to run on manager nodes for consistency:
```yaml
temporal:
  deploy:
    placement:
      constraints:
        - node.role == manager
```

## Alternative Connection Methods

### Via Traefik (if preferred)
If you want to use Traefik routing instead of direct connection:

1. Update service environment:
```powershell
$serviceEnv['TEMPORAL_HOST'] = 'temporal.localhost:80'
```

2. Ensure your Temporal SDK supports cleartext h2c through proxies

### Via Docker Host Internal
For containerized clients that can't use localhost:
```yaml
environment:
  - TEMPORAL_HOST=host.docker.internal:7233
```

## Summary

The key fixes implemented:
1. ✅ **Host mode networking** - bypasses Swarm routing mesh
2. ✅ **IPv4 forced addressing** - eliminates resolver issues  
3. ✅ **Enhanced healthchecks** - prevents ready-port-but-not-ready-service issues
4. ✅ **UTF-8 encoding fix** - resolves PowerShell mojibake error
5. ✅ **Traefik h2c support** - provides alternative routing option

These changes should eliminate the gRPC broken pipe errors and provide stable Temporal connectivity.
