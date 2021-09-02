USE [TPJ PROD]
GO
/****** Object:  StoredProcedure [kiosk].[FilterLeaveRequestThatNeedDocument]    Script Date: 09/02/21 5:16:22 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROC [kiosk].[FilterLeaveRequestThatNeedDocument](
	@PageCount INT OUT,
	@Personnel NVARCHAR(MAX) = NULL,
	@LeaveTypeID TINYINT = NULL,
	@IsExpired BIT = 0,
	@IsPending BIT = 0,
	@IsNoted BIT = 0,
	@IsApproved BIT = 0,
	@IsCancelled BIT = 0,
	@StartDate DATE = NULL,
	@EndDate DATE = NULL,
	@PageNumber INT = NULL,
	@GridCount INT = NULL
)
AS
BEGIN
	IF @Personnel = ''
		SET @Personnel = null
	IF @PageNumber IS NULL AND @GridCount IS NULL
	BEGIN
		RAISERROR('Some field is missing', 16, 2);
	END
	ELSE 
	BEGIN
		DECLARE @Sum INT = 0;
		IF @PageNumber > 1
			SET @Sum = (@PageNumber - 1) * @GridCount;
		
		SELECT DISTINCT @PageCount = COUNT(l.ID) 
		FROM kiosk.[Leave Request] l
			INNER JOIN hr.Personnel
				ON l.[Personnel ID] = Personnel.ID
					AND Personnel.Deleted = 0 
			INNER JOIN lookup.[Leave Type] lt
				ON l.[Leave Type ID] = lt.ID
					AND lt.[Has Document Needed] = 1 
		WHERE (@Personnel IS NULL OR CONCAT([Last Name], [First Name], [Middle Name]) LIKE CONCAT('%', @Personnel, '%'))
			AND (@LeaveTypeID IS NULL OR [Leave Type ID] = @LeaveTypeID)
			AND ((@IsPending = 1 AND ((Approved IS NULL OR Approved = 0) AND (Cancelled IS NULL OR Cancelled = 0)))
				OR (@IsNoted = 1 AND ([Noted By] IS NOT NULL AND [Noted On] IS NOT NULL))
				OR (@IsCancelled = 1 AND (Cancelled = 1 AND (Approved IS NULL OR Approved = 0)))
				OR (@IsApproved = 1 AND (Approved = 1 AND (Cancelled IS NULL OR Cancelled = 0)))
				OR (@IsExpired = 1 AND (CONVERT(INT, DATEDIFF(HOUR, l.[Created On], GETDATE())) >= 48 AND (Approved IS NULL OR Approved = 0) AND (Cancelled IS NULL OR Cancelled = 0))))
			AND (@StartDate IS NULL OR @EndDate IS NULL OR ([Requested Date] BETWEEN @StartDate AND @EndDate))

		SET @PageCount = CEILING(CONVERT(FLOAT, @PageCount) / CONVERT(FLOAT, @GridCount))

		SELECT DISTINCT l.*
		FROM kiosk.[Leave Request] l
			INNER JOIN hr.Personnel
				ON l.[Personnel ID] = Personnel.ID
					AND Personnel.Deleted = 0 
			INNER JOIN lookup.[Leave Type] lt
				ON l.[Leave Type ID] = lt.ID
					AND lt.[Has Document Needed] = 1 
		WHERE (@Personnel IS NULL OR CONCAT([Last Name], [First Name], [Middle Name]) LIKE CONCAT('%', @Personnel, '%'))
			AND (@LeaveTypeID IS NULL OR [Leave Type ID] = @LeaveTypeID)
			AND ((@IsPending = 1 AND ((Approved IS NULL OR Approved = 0) AND (Cancelled IS NULL OR Cancelled = 0)))
				OR (@IsNoted = 1 AND ([Noted By] IS NOT NULL AND [Noted On] IS NOT NULL))
				OR (@IsCancelled = 1 AND (Cancelled = 1 AND (Approved IS NULL OR Approved = 0)))
				OR (@IsApproved = 1 AND (Approved = 1 AND (Cancelled IS NULL OR Cancelled = 0)))
				OR (@IsExpired = 1 AND (CONVERT(INT, DATEDIFF(HOUR, l.[Created On], GETDATE())) >= 48 AND (Approved IS NULL OR Approved = 0) AND (Cancelled IS NULL OR Cancelled = 0))))
			AND (@StartDate IS NULL OR @EndDate IS NULL OR ([Requested Date] BETWEEN @StartDate AND @EndDate))
		ORDER BY l.[Requested Date] DESC
		OFFSET @Sum ROWS
		FETCH NEXT @GridCount 
		ROWS ONLY
	END
END
