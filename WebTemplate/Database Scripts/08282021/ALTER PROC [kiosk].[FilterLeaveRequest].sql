USE [TPJ PROD]
GO
/****** Object:  StoredProcedure [kiosk].[FilterLeaveRequest]    Script Date: 08/28/21 3:39:40 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROC [kiosk].[FilterLeaveRequest](
	@PageCount INT OUT,
	@PersonnelID BIGINT,
	@LeaveTypeID TINYINT = NULL,
	@IsExpired BIT = 0,
	@IsPending BIT = 0,
	@IsApproved BIT = 0,
	@IsCancelled BIT = 0,
	@StartDate DATE = NULL,
	@EndDate DATE = NULL,
	@PageNumber INT = NULL,
	@GridCount INT = NULL
)
AS
BEGIN
	IF @PageNumber IS NULL AND @GridCount IS NULL
	BEGIN
		RAISERROR('Some field is missing', 16, 2);
	END
	ELSE 
	BEGIN
		DECLARE @Sum INT = 0;
		IF @PageNumber > 1
			SET @Sum = (@PageNumber - 1) * @GridCount;
		
		SELECT @PageCount = COUNT(ID) 
		FROM kiosk.[Leave Request]  
		WHERE [Personnel ID] = @PersonnelID
			AND (@LeaveTypeID IS NULL OR [Leave Type ID] = @LeaveTypeID)
			AND ((@IsPending = 1 AND ((COALESCE(Approved, 0) = 0) AND (COALESCE(Cancelled, 0) = 0)))
				OR (@IsCancelled = 1 AND (COALESCE(Approved, 0) = 0))
				OR (@IsApproved = 1 AND (COALESCE(Cancelled, 0) = 0))
				OR (@IsExpired = 0 AND (CONVERT(INT, DATEDIFF(HOUR, [Leave Request].[Created On], GETDATE())) >= 48 AND (Approved IS NULL OR Approved = 0) AND (Cancelled IS NULL OR Cancelled = 0))))
			AND (@StartDate IS NULL OR @EndDate IS NULL OR ([Requested Date] BETWEEN @StartDate AND @EndDate))

		SET @PageCount = CEILING(CONVERT(FLOAT, @PageCount) / CONVERT(FLOAT, @GridCount))
		
		SELECT *
		FROM kiosk.[Leave Request]  
		WHERE [Personnel ID] = @PersonnelID
			AND (@LeaveTypeID IS NULL OR [Leave Type ID] = @LeaveTypeID)
			AND ((@IsPending = 1 AND ((COALESCE(Approved, 0) = 0) AND (COALESCE(Cancelled, 0) = 0)))
				OR (@IsCancelled = 1 AND (COALESCE(Approved, 0) = 0))
				OR (@IsApproved = 1 AND (COALESCE(Cancelled, 0) = 0))
				OR (@IsExpired = 0 AND (CONVERT(INT, DATEDIFF(HOUR, [Leave Request].[Created On], GETDATE())) >= 48 AND (Approved IS NULL OR Approved = 0) AND (Cancelled IS NULL OR Cancelled = 0))))
			AND (@StartDate IS NULL OR @EndDate IS NULL OR ([Requested Date] BETWEEN @StartDate AND @EndDate))
		ORDER BY [Requested Date] DESC
		OFFSET @Sum ROWS
		FETCH NEXT @GridCount ROWS ONLY
	END
END
