USE [TPJ PROD]
GO
/****** Object:  StoredProcedure [hr].[GetLeavesWithCredits]    Script Date: 09/01/21 6:56:15 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROC [hr].[GetLeavesWithCredits](
	@PersonnelID BIGINT,
	@Year SMALLINT
)
AS
BEGIN
	DECLARE @Gender bit = (SELECT [Gender] FROM hr.Personnel WHERE ID = @PersonnelID)

	SELECT DISTINCT lt.*
	FROM hr.[Personnel Leave Credits] lc
		INNER JOIN lookup.[Leave Type] lt
			ON lc.[Leave Type ID] = lt.ID AND lt.Deleted = 0
	WHERE lc.[Year Valid] = @Year
		AND lc.[Personnel ID] = @PersonnelID
		AND lc.[Leave Credits] > 0
		AND ((@Gender = 1 AND lt.ID <> 2) OR (@Gender = 0 AND lt.ID IS NOT NULL))
	ORDER BY lt.Description
END
