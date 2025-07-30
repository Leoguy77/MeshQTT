# Enhanced Access Control Features

This document demonstrates the new advanced access control features implemented in MeshQTT.

## Overview

The enhanced ACL system provides:

1. **Topic-level access control enforcement** - Both publish and subscribe operations are now validated against ACLs
2. **User Groups** - Users can belong to groups with inherited permissions
3. **User Roles** - Fine-grained permission system with hierarchical inheritance
4. **Time-based access controls** - Permissions can be restricted by time, day of week, etc.
5. **Dynamic configuration reloading** - All changes take effect without broker restart
6. **Backward compatibility** - Existing whitelist/blacklist configurations continue to work

## Configuration Examples

### Basic Group Configuration

```json
{
  "Groups": [
    {
      "Name": "MeshUsers",
      "Description": "Basic mesh network users",
      "Enabled": true,
      "SubscriptionTopicLists": {
        "WhitelistTopics": ["mesh/+/status", "mesh/+/position"],
        "BlacklistTopics": ["admin/#"]
      },
      "PublishTopicLists": {
        "WhitelistTopics": ["mesh/+/status"],
        "BlacklistTopics": ["admin/#", "system/#"]
      },
      "ThrottleUser": true,
      "MonthlyByteLimit": 1048576
    }
  ]
}
```

### Advanced Role Configuration

```json
{
  "Roles": [
    {
      "Name": "DataReader",
      "Description": "Can read sensor data and telemetry",
      "Enabled": true,
      "TopicPermissions": [
        {
          "TopicPattern": "sensor/+/data",
          "Permission": 1,
          "Priority": 100
        },
        {
          "TopicPattern": "telemetry/#",
          "Permission": 1,
          "Priority": 100
        }
      ]
    },
    {
      "Name": "DataWriter",
      "Description": "Can write sensor data",
      "Enabled": true,
      "InheritsFrom": ["DataReader"],
      "TopicPermissions": [
        {
          "TopicPattern": "sensor/+/data",
          "Permission": 3,
          "Priority": 200
        }
      ]
    }
  ]
}
```

### Time-based Access Controls

```json
{
  "Name": "ScheduledUser",
  "Description": "User with time-based restrictions",
  "Enabled": true,
  "TopicPermissions": [
    {
      "TopicPattern": "temp/#",
      "Permission": 3,
      "Priority": 100,
      "TimeRestriction": {
        "AllowedDaysOfWeek": [1, 2, 3, 4, 5],
        "AllowedHours": [
          { "StartHour": 9, "EndHour": 17 }
        ]
      }
    }
  ]
}
```

### User Configuration with Groups and Roles

```json
{
  "Users": [
    {
      "UserName": "meshuser1",
      "Password": "password123",
      "Groups": ["MeshUsers"],
      "Roles": ["DataReader"],
      "ValidateClientId": false,
      "TopicPermissions": [
        {
          "TopicPattern": "special/topic",
          "Permission": 3,
          "Priority": 2000
        }
      ]
    }
  ]
}
```

## Permission Types

- **None (0)**: No access
- **Read (1)**: Subscribe/read access only
- **Write (2)**: Publish/write access only  
- **ReadWrite (3)**: Both read and write access
- **Admin (7)**: Full administrative access

## Permission Resolution

Permissions are resolved in the following order (highest priority first):

1. **User-specific TopicPermissions** (priority + 1000)
2. **Group permissions** (priority + 100) 
3. **Role permissions** (base priority)
4. **Legacy whitelist/blacklist** (for users without roles/groups)

Within each category, permissions are resolved by:
1. **Priority** (higher number wins)
2. **Topic specificity** (more specific patterns win)

## Testing the Enhanced ACL System

### 1. Topic Matching Tests

The system correctly handles MQTT wildcards:
- `sensor/+/temperature` matches `sensor/1/temperature` but not `sensor/1/2/temperature`
- `sensor/#` matches `sensor/1/temperature` and `sensor/1/2/3/temperature` but not `sensor`
- `#` matches any topic

### 2. Hierarchical Inheritance Tests

- Roles can inherit from other roles using `InheritsFrom`
- Groups can inherit from other groups using `InheritsFrom`
- Users inherit permissions from their groups and roles
- User-specific permissions override inherited permissions

### 3. Time-based Restriction Tests

- Access can be restricted to specific days of the week
- Access can be restricted to specific hours
- Date ranges can be specified with StartTime/EndTime
- Current implementation respects UTC time

### 4. Priority Resolution Tests

When multiple permissions apply to the same topic:
- Higher priority permissions override lower priority ones
- More specific topic patterns override less specific ones
- User permissions override group/role permissions

## Dynamic Reloading

The configuration file is monitored for changes and automatically reloaded:
- FileSystemWatcher for real-time detection
- Polling fallback (every 1 second) for Docker environments
- All new ACL features support dynamic reloading
- No broker restart required

## Backward Compatibility

Existing configurations continue to work unchanged:
- Users with only `SubscriptionTopicLists` and `PublishTopicLists` use the legacy system
- Users with `Groups`, `Roles`, or `TopicPermissions` use the new system
- Mixed configurations are supported

## Security Enhancements

The enhanced ACL system provides better security through:

1. **Default deny**: If no permissions match, access is denied
2. **Explicit permissions**: Clear definition of what each user can do
3. **Audit trail**: All permission checks are logged
4. **Time-based controls**: Temporary or scheduled access
5. **Hierarchical management**: Easy to manage permissions for groups of users

## Example Use Cases

### IoT Sensor Network
- **SensorReaders** role: Read sensor data and telemetry
- **SensorWriters** role: Write calibration commands
- **DeviceAdmins** group: Full device management access
- **Technicians** group: Limited access during business hours

### Mesh Communication Network
- **PublicUsers** group: Access to public channels only
- **PrivateUsers** group: Access to private channels
- **Moderators** role: Can manage public channels
- **Administrators** role: Full system access

### Development/Production Split
- **Developers** group: Full access to dev/# topics
- **Production** group: Read-only access to prod/# topics
- **Operators** role: Write access to prod/control/# during business hours
- **Auditors** role: Read-only access to all audit logs

This enhanced ACL system provides enterprise-grade access control while maintaining the simplicity and performance expected from MeshQTT.