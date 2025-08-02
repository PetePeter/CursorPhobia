# WI#7 User Acceptance Testing Checklist

## Manual UAT Items for GitHub Issue #7

**For the user to validate when they wake up - these items should be tested in the actual environment:**

---

## 1. DPI Scaling Validation

### Test Scenario: Mixed-DPI Monitor Setup
- [ ] **Connect monitors with different DPI scales** (e.g., 4K + 1080p)
- [ ] **Launch CursorPhobia** and verify it detects both monitors correctly
- [ ] **Test cursor positioning accuracy:**
  - [ ] Move cursor between monitors - should transition smoothly without jumps
  - [ ] Window positioning should be accurate on both monitors
  - [ ] Proximity detection should work consistently across DPI boundaries
- [ ] **Expected Result:** No cursor "teleporting" or incorrect positioning on mixed-DPI setups

---

## 2. Monitor Hotplug Detection

### Test Scenario: Dynamic Monitor Configuration
- [ ] **Start CursorPhobia** on single monitor setup
- [ ] **Connect additional monitor** while application is running
- [ ] **Verify automatic detection:**
  - [ ] No restart required
  - [ ] New monitor appears in Settings → Multi-Monitor tab within seconds
  - [ ] Configuration automatically adapts to new layout
- [ ] **Disconnect monitor** and verify removal detection
- [ ] **Expected Result:** Seamless adaptation to monitor changes without user intervention

---

## 3. Per-Monitor Configuration

### Test Scenario: Individual Monitor Settings
- [ ] **Open Settings → Multi-Monitor tab**
- [ ] **Verify monitor list** shows all connected monitors with descriptive names
- [ ] **Configure different settings per monitor:**
  - [ ] Set different proximity thresholds for each monitor
  - [ ] Set different push distances for each monitor  
  - [ ] Test enable/disable individual monitors
- [ ] **Test settings persistence:**
  - [ ] Close and reopen application - settings should be preserved
  - [ ] Disconnect/reconnect monitors - settings should migrate properly
- [ ] **Expected Result:** Individual monitor behavior as configured

---

## 4. Real-World Vineyard Operations Testing

### Test Scenario: Professional Mixed-Monitor Workflow
- [ ] **Setup vineyard-typical configuration:**
  - [ ] High-resolution monitor (4K) for GIS/mapping software
  - [ ] Standard monitor (1080p) for productivity/communication
- [ ] **Test precision agriculture workflow:**
  - [ ] Open detailed vineyard mapping on 4K monitor
  - [ ] Use productivity applications on 1080p monitor
  - [ ] Verify cursor behavior is appropriate for each monitor's purpose
- [ ] **Test mobile field operations:**
  - [ ] Connect/disconnect portable display during operation
  - [ ] Verify no disruption to active data monitoring
- [ ] **Expected Result:** Consistent, appropriate behavior for professional workflows

---

## 5. Settings UI Validation

### Test Scenario: Per-Monitor Configuration Interface
- [ ] **Navigate to Settings → Multi-Monitor tab**
- [ ] **Verify UI elements:**
  - [ ] Monitor list shows connected monitors with clear identification
  - [ ] Per-monitor settings panel updates when selecting different monitors
  - [ ] "Use Global Settings" checkbox provides simple configuration path
  - [ ] Settings changes apply immediately (real-time preview)
- [ ] **Test different configuration scenarios:**
  - [ ] Global settings mode vs. per-monitor customization
  - [ ] Enable/disable individual monitors
  - [ ] Custom proximity/push settings validation
- [ ] **Expected Result:** Intuitive, responsive configuration interface

---

## 6. Performance and Stability Validation  

### Test Scenario: Extended Operation
- [ ] **Run CursorPhobia for extended period** (several hours)
- [ ] **Monitor system performance:**
  - [ ] CPU usage should remain low
  - [ ] Memory usage should remain stable
  - [ ] No unusual system slowdowns
- [ ] **Test under load:**
  - [ ] Rapid monitor connection/disconnection cycles
  - [ ] Heavy multitasking with precision agriculture software
  - [ ] Multiple vineyard monitoring applications running simultaneously
- [ ] **Expected Result:** Stable, performant operation under real-world load

---

## 7. Error Recovery Testing

### Test Scenario: Edge Cases and Error Conditions
- [ ] **Test monitor driver updates:**
  - [ ] Update monitor drivers while CursorPhobia is running
  - [ ] Verify graceful handling of driver changes
- [ ] **Test system sleep/wake cycles:**
  - [ ] Put system to sleep with multiple monitors
  - [ ] Wake system and verify monitor detection
- [ ] **Test monitor power cycling:**
  - [ ] Turn monitors off/on independently
  - [ ] Verify detection and settings preservation
- [ ] **Expected Result:** Graceful recovery from all error conditions

---

## 8. Integration Testing

### Test Scenario: Ecosystem Compatibility
- [ ] **Test with vineyard software stack:**
  - [ ] GIS mapping applications
  - [ ] Weather monitoring systems
  - [ ] Harvest coordination tools
  - [ ] Communication/productivity software
- [ ] **Verify no conflicts or interference:**
  - [ ] Other applications should work normally
  - [ ] No cursor conflicts with other mouse enhancement software
  - [ ] System-wide mouse behavior unaffected when CursorPhobia disabled
- [ ] **Expected Result:** Seamless integration with existing software ecosystem

---

## Success Criteria Summary

For WI#7 to be considered fully validated:

### Critical Success Indicators:
- [ ] **DPI Scaling:** Mixed-DPI setups work without cursor positioning issues
- [ ] **Hotplug Detection:** Monitor changes detected automatically, no restart required
- [ ] **Per-Monitor Settings:** Individual monitor configuration works and persists
- [ ] **Performance:** No noticeable performance degradation vs. single-monitor operation
- [ ] **Stability:** Extended operation remains stable under real-world usage
- [ ] **User Experience:** Professional workflows enhanced, not disrupted

### Quality Gates:
- [ ] **No regressions:** All existing single-monitor functionality continues to work
- [ ] **Error handling:** Graceful recovery from all tested error conditions
- [ ] **Settings persistence:** Configuration survives all hotplug scenarios tested
- [ ] **UI responsiveness:** Settings interface remains responsive under all conditions

---

## Validation Results

**Date Tested:** _____________  
**Tested By:** _____________  
**Environment:** _____________  

**Overall Assessment:** 
- [ ] ✅ ALL TESTS PASS - Ready for production use
- [ ] ⚠️ MINOR ISSUES - Ready with documented limitations  
- [ ] ❌ MAJOR ISSUES - Additional development required

**Comments/Issues Found:**
```
[Space for notes on any issues discovered during UAT]
```

---

**Testing Complete:** This checklist validates that all critical multi-monitor functionality works correctly in real-world vineyard operations scenarios, confirming the technical debt has been successfully resolved.