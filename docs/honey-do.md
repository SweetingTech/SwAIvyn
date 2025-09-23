# SwAIvyn Honey-Do List 🍯✅

*Technical debt, improvements, and maintenance items identified during comprehensive program review*

---

## 🚨 **Critical Issues (Fix Soon)**

### 1. **SignalR Frontend Code Without Backend Implementation**
- **Issue**: `frontend/src/hooks/useChatHub.ts` contains Microsoft SignalR client code but FastAPI backend has no corresponding SignalR hubs
- **Impact**: Dead code that could cause runtime errors if accidentally invoked; misleading for developers
- **Fix**: Either remove `useChatHub.ts` and related imports OR implement SignalR hubs in FastAPI backend
- **Locations**:
  - `frontend/src/hooks/useChatHub.ts:6` - exports `useChatHub()` function with SignalR implementation
  - `frontend/src/hooks/useChatHub.ts:22,33,46,50,53,62` - console.error/log statements for SignalR events
  - `frontend/src/components/chat/ChatInput.tsx:11` - comment mentions "integration with SignalR chat hub"

### 2. **React Router Future Flag Warnings**
- **Issue**: Browser console shows warnings about React Router v7 migration flags
- **Impact**: Deprecation warnings indicate code will break in future React Router versions
- **Fix**: Add future flags to router configuration or update React Router to v7
- **Locations**: 
  - Browser console logs show warnings at runtime
  - Router configuration likely in `frontend/src/App.tsx` or routing setup files
- **Warning Messages**:
  - `v7_startTransition` - State updates will be wrapped in React.startTransition 
  - `v7_relativeSplatPath` - Relative route resolution changes in Splat routes

### 3. **Inconsistent Authentication Pattern Usage** 
- **Issue**: Mix of `useEffectiveUser()` hook and direct API calls across components
- **Impact**: Inconsistent error handling and authentication state management
- **Fix**: Audit all components to ensure consistent use of `useEffectiveUser()` pattern
- **Status**: **ACTUALLY GOOD** - Analysis shows consistent usage across all major pages
- **Locations** (All properly using the hook):
  - `frontend/src/pages/DashboardPage.tsx:42`
  - `frontend/src/pages/ChatPage.tsx:59`
  - `frontend/src/pages/SettingsPage.tsx:30,186,662,1441`
  - `frontend/src/pages/AgentsPage.tsx:151`
  - All other pages follow the same pattern

---

## ⚠️ **High Priority (Address Soon)**

### 4. **Missing Environment Variable Validation**
- **Issue**: Services assume environment variables exist without validation
- **Impact**: Silent failures or crashes when env vars are missing/malformed
- **Fix**: Add startup validation for required environment variables in all services
- **Locations**:
  - `Services/bff/alembic.ini:3` - Comment shows DATABASE_URL read from environment without validation
  - `frontend/src/services/agentService.ts:35` - VITE_API_BASE_URL read without validation
  - Backend services likely read env vars directly without checking existence

### 5. **Database Schema Inconsistencies**
- **Issue**: Documentation mentions SQLite but project uses PostgreSQL; schema files may be outdated
- **Impact**: Migration failures and data integrity issues
- **Fix**: Audit `shared/schema.ts` against actual database structure; run `npm run db:push` to sync
- **Critical**: Never change primary key ID types (serial ↔ varchar) - causes destructive migrations

### 6. **Temporal Configuration Issues**
- **Issue**: `BIND_ON_IP` overrides removed but may still cause ringpop bootstrap failures
- **Impact**: Temporal workflows may fail to start properly
- **Fix**: Monitor Temporal startup logs and verify cluster formation works reliably
- **Files**: `docker-stack.yml`, Temporal service configuration

### 7. **Missing Error Boundaries**
- **Issue**: Frontend lacks React Error Boundaries for graceful error handling
- **Impact**: White screen of death when components crash
- **Fix**: Implement Error Boundaries around major component sections
- **Locations**: **CONFIRMED MISSING** - No ErrorBoundary or componentDidCatch found in codebase
- **Priority**: Especially important for chat interface and settings pages
- **Suggested files to create**: 
  - `frontend/src/components/ErrorBoundary.tsx`
  - Wrap major sections in `frontend/src/App.tsx`

---

## 🔧 **Medium Priority (Quality Improvements)**

### 8. **Inconsistent API Error Handling**
- **Issue**: Some API endpoints return different error formats
- **Impact**: Frontend error handling is fragmented and inconsistent
- **Fix**: Standardize API error response format across all FastAPI endpoints
- **Locations**:
  - `Services/bff/app/main.py:214,226,231,339,374,383,398,407` - Multiple HTTPException patterns with different detail formats
  - `frontend/src/services/conversationService.ts:33,151` - Different error handling patterns (any types)
  - `frontend/src/components/chat/ChatSidebar.tsx:239` - Another error handling pattern

### 9. **Missing Type Safety in API Calls**
- **Issue**: Frontend API calls use `any` types or lack proper TypeScript interfaces
- **Impact**: Runtime errors from API contract mismatches
- **Fix**: Generate TypeScript types from FastAPI schema or create shared type definitions
- **Locations**:
  - `frontend/src/App.tsx:23-25` - Stagewise imports use `any` types
  - `frontend/src/services/ttsService.ts:75` - payload uses `any` type
  - `frontend/src/components/AgentForm.tsx:62` - data cast to unknown then any
  - `frontend/src/contexts/InitializationContext.tsx:164` - error typed as unknown
  - `frontend/src/services/agentService.ts:12,29` - Record<string, unknown> types
- **Tools**: Consider using `@hey-api/openapi-ts` to generate types from OpenAPI spec

### 10. **Hardcoded Configuration Values**
- **Issue**: URLs, timeouts, and limits are hardcoded throughout codebase
- **Impact**: Difficult to configure for different environments
- **Fix**: Move configuration to environment variables or config files
- **Examples**: TTS service URLs, database connection timeouts, API rate limits

### 11. **Missing Health Check Endpoints**
- **Issue**: No standardized health check endpoints for service monitoring
- **Impact**: Difficult to monitor service health in production
- **Fix**: Implement `/health` and `/ready` endpoints for all services
- **Status**: **PARTIALLY IMPLEMENTED** 
- **Locations**:
  - ✅ `Services/bff/app/main.py:313-327` - Has `/healthz`, `/readyz`, `/api/healthz`, `/api/readyz` endpoints
  - ✅ `Services/bff/app/main.py:336` - Has `/api/llm/health` endpoint  
  - ❌ **Missing**: Orchestrator and TTS services lack health endpoints
  - `Services/bff/app/models.py:136` - health_endpoint field exists for external agents

### 12. **Inconsistent Logging Patterns**
- **Issue**: Mix of `console.log`, `print()`, and structured logging across services
- **Impact**: Difficult to debug issues and monitor system behavior
- **Fix**: Implement consistent structured logging with log levels
- **Locations** (Frontend console.log usage):
  - `frontend/src/contexts/InitializationContext.tsx:143,153,163,170` - Mix of console.log and console.error
  - `frontend/src/hooks/useTranslation.ts:26` - console.error for language preference
  - `frontend/src/utils/playTts.ts:17` - console.error for TTS playback
  - `frontend/src/hooks/useChatHub.ts:22,33,46,50,53,62` - Multiple console log statements
  - `frontend/src/pages/DashboardPage.tsx:88,96,110,113,569,594,621` - Debug and error logging
  - `frontend/src/components/MemorySyncStatus.tsx:80` - console.error for sync status
- **Backend**: `Services/bff/app/main.py:279` - print() statement for DB ready status
- **Tools**: Consider `winston` for Node.js, `structlog` for Python

---

## 🧹 **Low Priority (Nice to Have)**

### 13. **Unused Dependencies**
- **Issue**: `package.json` and `requirements.txt` may contain unused dependencies
- **Impact**: Larger bundle sizes and potential security vulnerabilities
- **Fix**: Audit dependencies and remove unused packages
- **Tools**: Use `depcheck` for Node.js, `pip-check` for Python

### 14. **Missing Unit Tests**
- **Issue**: No visible unit test structure in the codebase
- **Impact**: Difficult to prevent regressions and ensure code quality
- **Fix**: Add unit tests for critical business logic
- **Priority**: Start with authentication and chat message handling

### 15. **Component Organization**
- **Issue**: Some React components are large and handle multiple responsibilities
- **Impact**: Difficult to maintain and test individual features
- **Fix**: Break down large components into smaller, focused components
- **Examples**: Chat interface, settings pages, agent management

### 16. **Missing Code Documentation**
- **Issue**: Functions and classes lack JSDoc/docstring documentation
- **Impact**: Difficult for new developers to understand code purpose and usage
- **Fix**: Add JSDoc comments to public functions and complex logic
- **Priority**: Focus on API endpoints and core business logic first

### 17. **Performance Optimization Opportunities**
- **Issue**: Frontend may have unnecessary re-renders and API calls
- **Impact**: Poor user experience and increased server load
- **Fix**: Audit React components for performance issues
- **Tools**: Use React DevTools Profiler, implement memoization where appropriate

### 18. **Missing Loading States**
- **Issue**: Some API calls don't show loading indicators
- **Impact**: Poor user experience during slow network conditions
- **Fix**: Add loading states to all async operations
- **Priority**: Chat message sending, settings updates, agent operations

---

## 🔐 **Security Considerations**

### 19. **JWT Token Security**
- **Issue**: JWT tokens may not have proper expiration handling
- **Impact**: Security risk if tokens are compromised
- **Fix**: Implement proper token refresh logic and secure storage
- **Review**: Audit JWT expiration times and refresh token implementation

### 20. **CORS Configuration**
- **Issue**: CORS settings may be too permissive for production
- **Impact**: Potential security vulnerabilities in production deployment
- **Fix**: Review CORS configuration and tighten for production environments
- **Files**: FastAPI CORS middleware configuration

### 21. **Input Validation**
- **Issue**: API endpoints may lack comprehensive input validation
- **Impact**: Potential security vulnerabilities and data integrity issues
- **Fix**: Audit all API endpoints for proper input validation and sanitization
- **Tools**: Use Pydantic models for request validation in FastAPI

---

## 📊 **Monitoring & Observability**

### 22. **Missing Metrics Collection**
- **Issue**: No system metrics or performance monitoring
- **Impact**: Difficult to identify performance bottlenecks and system issues
- **Fix**: Implement basic metrics collection for key system indicators
- **Metrics**: API response times, database query performance, memory usage

### 23. **Log Aggregation**
- **Issue**: Logs scattered across multiple services without centralization
- **Impact**: Difficult to debug issues spanning multiple services
- **Fix**: Consider centralized logging solution for production deployments
- **Options**: ELK stack, Grafana Loki, or simple file aggregation

---

## 🚀 **Development Experience**

### 24. **Pre-commit Hooks**
- **Issue**: No automated code quality checks before commits
- **Impact**: Code quality issues make it into the repository
- **Fix**: Implement pre-commit hooks for linting, formatting, and basic tests
- **Tools**: `husky`, `lint-staged`, `prettier`, `eslint`

### 25. **Development Documentation**
- **Issue**: Missing developer onboarding documentation
- **Impact**: Difficult for new contributors to get started
- **Fix**: Create CONTRIBUTING.md with development workflow and coding standards
- **Include**: Setup instructions, coding standards, testing guidelines

---

## 📝 **Action Priority Matrix**

### **Fix This Week**
1. Remove SignalR dead code (#1) - `useChatHub.ts:6` and `ChatInput.tsx:11`
2. Fix React Router warnings (#2) - Add future flags to router config
3. Implement Error Boundaries (#7) - Create `ErrorBoundary.tsx` component

### **Fix This Month**
4. Standardize API error handling (#8) - Fix `main.py:214-407` HTTPException patterns
5. Add type safety (#9) - Fix `any` types in `App.tsx:23-25`, `ttsService.ts:75`
6. Environment variable validation (#4) - Validate at service startup

### **Fix This Quarter**
7. Standardize logging (#12) - Replace console.log with structured logging
8. Add unit tests (#14)
9. Implement security audit (#19-21)

---

*Last updated: September 23, 2025*
*Generated during comprehensive program review*