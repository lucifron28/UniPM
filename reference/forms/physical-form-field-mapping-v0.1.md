# Physical Form Field Mapping v0.1

## Status

- **Status:** Provisional
- **Source:** Supplied blank General Services Department physical forms
- **Available coverage:** Page 1 of 2 only
- **Official completed samples:** Unavailable
- **Acknowledgement rules:** Confirmed for the digital workflow: whole-form
  acknowledgement is captured through the skilled worker's authenticated mobile
  session; the department head does not require a UniPM account.
- **RMRF rules:** Outside UniPM. The system prepares an acknowledged
  corrective-action handoff only; GSD manually encodes it in the existing WMS.

No source photographs are committed to this repository. The synthetic fixture
contains no real personnel names or signatures. This document records visible
fields only; it does not establish final production database contracts.

## Fire Extinguishers Monitoring Form

Visible fields:

- building or department
- quarter and year
- fire extinguisher number
- location
- type
- capacity
- expiration date
- operational yes/no status
- date inspected
- remarks
- actions and recommendations
- inspector
- department-head acknowledgement
- PPF supervisor notation

## Fire Alarm Preventive Maintenance Form

Visible fields:

- building or department
- semester and academic year
- device particulars or number
- location
- operational yes/no status
- date inspected
- remarks
- actions and recommendations
- inspector
- department-head acknowledgement
- electrical engineer role

## Emergency Lights Preventive Maintenance Form

Visible fields:

- building or department
- semester and academic year
- emergency light unit number
- date installed
- location
- operational yes/no status
- date inspected
- remarks
- actions and recommendations
- inspector
- department-head acknowledgement
- electrical engineer role

## Water Drinking Station Preventive Maintenance Form

Visible fields:

- quarter and year
- water drinking station number
- location
- operational yes/no status
- remarks
- replace carbon filter
- replace sediment filter
- check UV light
- RMRF number
- date accomplished
- department-head acknowledgement
- actions and recommendations
- plumber role
- PPF supervisor role

## Confirmed Digital Mapping

One digital form represents one existing one-page form and may contain multiple
asset inspection rows. Its lifecycle is `Draft -> Submitted -> Acknowledged`.
Only acknowledgement completes linked schedules and makes rows eligible for
official history and retrieval. The signatory name, position, and signature are
form data only and never retrieval, embedding, prompt, or handoff data.

## Pending Clarification

Page 2 fields, official completed samples, official location lists, and the
full mapping from remaining paper-form fields to digital records require further
confirmation. The current `categoryDetails` and `formData` fixture fields are
retained only as provisional development metadata and are not confirmed
production schemas.
