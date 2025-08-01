# WI#7 Multi-Monitor Support - Phased Implementation Plan

## Overview
This document outlines a 4-phase implementation plan to address the critical missing features in WI#3 Multi-Monitor Support. The plan prioritizes operational blockers while building foundational infrastructure for comprehensive multi-monitor support.

**Total Estimated Effort:** 6-9 development days  
**Risk Reduction:** MEDIUM-HIGH → LOW after completion

## Priority Assessment
Based on stakeholder feedback:
1. **Priority 1:** DPI Scaling Support (operational blocker)
2. **Priority 2:** MockMonitorManager (testing blocker) 
3. **Priority 3:** Monitor Hotplug Detection (operational blocker)
4. **Priority 4:** Thread Safety & Per-Monitor Config (stability)

---

## Phase 1: DPI Foundation & Testing Infrastructure
**Duration:** 2-3 days  
**Risk:** LOW  
**Dependencies:** None

### Objectives
- Implement DPI awareness APIs and core infrastructure
- Create MockMonitorManager for comprehensive testing
- Establish foundation for all subsequent monitor operations

### Deliverables

#### 1.1 DPI Support Infrastructure
- [ ] Add Windows DPI APIs to `WindowsAPI` namespace:
  - `GetDpiForMonitor()` 
  - `GetSystemMetrics()` for DPI values
  - `SetProcessDpiAwareness()` initialization
- [ ] Create `DpiInfo` model class with:
  - DPI values (X/Y)
  - Scale factors
  - Conversion utilities (logical ↔ physical pixels)
- [ ] Update `MonitorInfo` model to include DPI properties
- [ ] Implement DPI-aware coordinate conversion utilities

#### 1.2 Testing Infrastructure  
- [ ] Create `IMonitorManager` interface from existing `MonitorManager`
- [ ] Implement `MockMonitorManager` for unit testing:
  - Configurable monitor setups
  - DPI simulation capabilities  
  - Hotplug event simulation
- [ ] Update existing monitor tests to use interface
- [ ] Add comprehensive DPI conversion unit tests

### Success Criteria
- [ ] All existing monitor tests pass with new interface
- [ ] DPI coordinate conversions accurate within 1 pixel
- [ ] MockMonitorManager supports complex multi-monitor scenarios
- [ ] Code coverage for monitor operations ≥ 80%

### Testing Requirements
- Unit tests for all DPI conversion functions
- Integration tests with MockMonitorManager
- Validation of DPI values against known monitor configurations

---

## Phase 2: Monitor Hotplug Detection 
**Duration:** 1-2 days  
**Risk:** LOW-MEDIUM  
**Dependencies:** Phase 1 (DPI infrastructure)

### Objectives
- Implement real-time monitor configuration change detection
- Enable automatic adaptation to display changes without restart
- Integrate with existing configuration system

### Deliverables

#### 2.1 Event Detection System
- [ ] Add `WM_DISPLAYCHANGE` message handling to main message loop
- [ ] Implement `MonitorConfigurationWatcher` service:
  - Event-driven monitor list refresh
  - Change detection and notification
  - Integration with existing caching mechanism
- [ ] Create `MonitorChangeEventArgs` with change details:
  - Added/removed monitors
  - DPI changes
  - Resolution changes
  - Primary monitor changes

#### 2.2 Configuration Integration
- [ ] Update `MonitorManager` to subscribe to hotplug events
- [ ] Implement automatic cache invalidation on monitor changes
- [ ] Add monitor change notifications to configuration system
- [ ] Update per-monitor settings to handle dynamic monitor changes

### Success Criteria
- [ ] Monitor changes detected within 500ms of Windows notification
- [ ] Existing functionality unaffected during monitor changes
- [ ] Per-monitor settings properly migrate during topology changes
- [ ] No memory leaks from event subscriptions

### Testing Requirements
- Unit tests using MockMonitorManager hotplug simulation
- Integration tests with simulated monitor add/remove scenarios
- Performance tests ensuring responsive change detection

---

## Phase 3: Per-Monitor Configuration Integration
**Duration:** 2-3 days  
**Risk:** MEDIUM  
**Dependencies:** Phase 1 (DPI), Phase 2 (Hotplug)

### Objectives
- Fully integrate per-monitor settings with core engine
- Implement DPI-aware proximity calculations
- Enable monitor-specific behavior customization

### Deliverables

#### 3.1 Configuration System Integration
- [ ] Update `CursorPhobiaEngine` to use per-monitor settings:
  - Monitor-specific proximity thresholds
  - Monitor-specific push distances
  - Monitor-specific enable/disable states
- [ ] Implement DPI-aware proximity calculations:
  - Convert logical distances to physical pixels
  - Account for different DPI scales across monitors
  - Maintain consistent user experience across displays

#### 3.2 Settings Management
- [ ] Update `SettingsForm` to show per-monitor configuration UI
- [ ] Implement monitor identification in settings:
  - Display monitor names/positions
  - Visual monitor layout representation
  - Live preview of settings changes
- [ ] Add configuration validation for multi-monitor setups

#### 3.3 Engine Core Updates
- [ ] Update `ProximityDetector` for DPI-aware calculations
- [ ] Modify `WindowPusher` to respect per-monitor settings
- [ ] Update `EdgeWrapHandler` for proper cross-monitor wrapping
- [ ] Ensure thread-safe access to per-monitor configurations

### Success Criteria
- [ ] Different proximity thresholds work correctly on different monitors
- [ ] DPI scaling maintains consistent behavior across mixed-DPI setups
- [ ] Per-monitor settings persist through hotplug events
- [ ] Settings UI accurately reflects current monitor configuration

### Testing Requirements
- Mixed-DPI scenario testing (100%, 125%, 150%, 200% scales)
- Per-monitor behavior validation tests
- Configuration persistence tests during monitor changes
- UI responsiveness tests with live settings updates

---

## Phase 4: Thread Safety & Production Hardening
**Duration:** 1-2 days  
**Risk:** LOW  
**Dependencies:** Phase 3 (full integration)

### Objectives
- Resolve thread safety issues in monitor caching
- Implement comprehensive error handling
- Achieve production-ready stability and performance

### Deliverables

#### 4.1 Thread Safety Improvements
- [ ] Add proper locking to `MonitorManager` cache operations:
  - Reader-writer locks for cache access
  - Thread-safe monitor enumeration
  - Atomic cache updates during hotplug events
- [ ] Review and fix concurrent access patterns in:
  - Configuration updates
  - Monitor change notifications
  - DPI calculation caching

#### 4.2 Error Handling & Resilience
- [ ] Implement comprehensive error handling for:
  - DPI API failures (older Windows versions)
  - Monitor enumeration failures
  - Invalid monitor handles
  - Race conditions during hotplug events
- [ ] Add logging and diagnostics for monitor operations
- [ ] Implement graceful degradation for unsupported scenarios

#### 4.3 Performance Optimization
- [ ] Optimize DPI calculation caching
- [ ] Minimize allocation during frequent operations
- [ ] Review and optimize monitor change detection performance
- [ ] Add performance metrics collection

### Success Criteria
- [ ] No race conditions in concurrent scenarios (tested under load)
- [ ] Graceful handling of all error conditions
- [ ] Performance impact ≤ 5% compared to single-monitor operation
- [ ] Memory usage stable during extended multi-monitor operation

### Testing Requirements
- Stress testing with frequent monitor configuration changes
- Concurrent access testing with multiple threads
- Long-running stability tests (24+ hours)
- Memory leak detection and performance profiling

---

## Implementation Guidelines

### Development Approach
1. **Test-Driven Development**: Write tests before implementation
2. **Interface-First Design**: Define interfaces before concrete implementations  
3. **Incremental Integration**: Each phase builds on previous foundations
4. **Backward Compatibility**: Maintain existing single-monitor functionality

### Quality Gates
- **Code Coverage:** ≥ 80% for all new code
- **Performance:** No regression in single-monitor scenarios
- **Stability:** Pass 24-hour stress tests
- **Compatibility:** Support Windows 10/11, multiple DPI scales

### Risk Mitigation
- **Phase 1** establishes testing infrastructure early
- **MockMonitorManager** enables comprehensive testing without hardware
- **Incremental delivery** allows early validation and feedback
- **Comprehensive logging** enables production troubleshooting

---

## Phase Dependencies

```
Phase 1 (DPI + Testing)
    ↓
Phase 2 (Hotplug Detection)
    ↓  
Phase 3 (Configuration Integration)
    ↓
Phase 4 (Thread Safety + Hardening)
```

Each phase can be independently tested and delivered, enabling iterative development and early value delivery.

---

## Completion Criteria

### Phase 1 Complete
- [ ] DPI APIs implemented and tested
- [ ] MockMonitorManager enables full test coverage
- [ ] All existing tests pass with new interfaces

### Phase 2 Complete  
- [ ] Monitor hotplug detection working reliably
- [ ] No restart required for monitor configuration changes
- [ ] Automatic cache invalidation functioning

### Phase 3 Complete
- [ ] Per-monitor settings fully functional
- [ ] DPI-aware proximity calculations working
- [ ] Mixed-DPI setups supported seamlessly

### Phase 4 Complete
- [ ] Thread safety issues resolved
- [ ] Production-ready error handling
- [ ] Performance and stability validated

### Overall Success
- [ ] All critical issues from issue #7 resolved
- [ ] Test coverage ≥ 80% for multi-monitor functionality
- [ ] Mixed-DPI environments fully supported
- [ ] Monitor hotplug events handled gracefully
- [ ] Per-monitor configuration working end-to-end
- [ ] Production-ready stability and performance